using FluentValidation;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Common;

namespace ORP.Application.Assignments.Assign;

public sealed class AssignMessageValidator : AbstractValidator<AssignMessageRequest>
{
    public AssignMessageValidator() { RuleFor(x => x.AssignedTo).GreaterThan(0); }
}

public sealed class AssignMessageHandler(IORPStore store, IUserAuthorizationQueries accessService,
    IValidator<AssignMessageRequest> validator, ICurrentUser user, ICorrelationContext correlation,
    AssignmentCoordinator assignments, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public async Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
        => await HandleCoreAsync(messageId, request, false, cancellationToken);

    public async Task ReassignAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
        => await HandleCoreAsync(messageId, request, true, cancellationToken);

    private Task HandleCoreAsync(long messageId, AssignMessageRequest request, bool reassign,
        CancellationToken cancellationToken)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, ct);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            if (reassign && message.CurrentAssigneeId is null)
                throw new DomainRuleViolationException("An unassigned message must be assigned before it can be reassigned.");
            if (!reassign && message.CurrentAssigneeId is not null)
                throw new DomainRuleViolationException("An assigned message must be reassigned instead of assigned.");
            var reviewLevel = ReviewAssignmentRules.AssignmentLevelForState(message.State)
                ?? throw new DomainRuleViolationException($"Assignment is not allowed while message is in state '{message.State}'.");
            var workflow = await store.FindWorkflowAsync(message.WorkflowDefinitionId, ct)
                ?? throw new ResourceNotFoundException("Workflow was not found.");
            if (!workflow.IsActive || !workflow.RequiredLevels().Contains(reviewLevel))
                throw new DomainRuleViolationException("This workflow has no assignable confirmation at the current level.");
            var source = access.Source;
            var target = await accessService.CheckAsync(request.AssignedTo, source.BranchId, message.ProcessingDepartmentIds,
                ReviewAssignmentRules.PermissionForLevel(reviewLevel), ct)
                ?? throw new ResourceNotFoundException("Assignee was not found.");
            var reviews = access.Reviews;
            var allowSelfAssignment = workflow.AllowSelfAssignment;
            if (!ReviewAssignmentRules.IsEligible(request.AssignedTo, target,
                    workflow.AllowReviewerReuse ? [] : ReviewAssignmentRules.ApprovedReviewerIds(reviews), user.UserId, message.CurrentAssigneeId, allowSelfAssignment))
                throw new ValidationException("The assignee is not eligible to review the message in its current workflow state.");
            await assignments.AssignAsync(message, request.AssignedTo, user.UserId, correlation.CorrelationId,
                ct, access.IsGlobalAdministrator || allowSelfAssignment);
            await store.SaveChangesAsync(ct);
        }, cancellationToken);
}
