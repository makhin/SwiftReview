using FluentValidation;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Common;

namespace ORP.Application.Assignments.Assign;

public sealed class AssignMessageValidator : AbstractValidator<AssignMessageRequest>
{
    public AssignMessageValidator()
    {
        RuleFor(x => x.AssignedTo).GreaterThan(0);
    }
}

public sealed class AssignMessageHandler(IORPStore store, IUserAuthorizationQueries accessService,
    IValidator<AssignMessageRequest> validator, ICurrentUser user, ICorrelationContext correlation,
    AssignmentCoordinator assignments, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken) =>
        transactions.ExecuteAsync(ct => HandleInTransactionAsync(messageId, request, ct), cancellationToken);

    // The caller must own the transaction; shared with assign-and-confirm.
    internal async Task HandleInTransactionAsync(long messageId, AssignMessageRequest request, CancellationToken ct)
    {
        var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, ct);
        await validator.ValidateAndThrowAsync(request, ct);
        var message = access.Message;
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
        if (!ReviewAssignmentRules.IsEligible(request.AssignedTo, target,
                workflow.AllowReviewerReuse ? [] : ReviewAssignmentRules.ApprovedReviewerIds(reviews), message.CurrentAssigneeId))
            throw new ValidationException("The assignee is not eligible to review the message in its current workflow state.");
        await assignments.AssignAsync(message, request.AssignedTo, user.UserId, correlation.CorrelationId,
            ct);
        await store.SaveChangesAsync(ct);
    }
}
