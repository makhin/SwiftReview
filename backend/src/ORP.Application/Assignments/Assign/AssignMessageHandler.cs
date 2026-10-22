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
        RuleFor(x => x.ExpectedVersion).Must(IsVersion).WithMessage("expectedVersion must be a base64 SQL rowversion (8 bytes).");
    }

    private static bool IsVersion(string? value)
    {
        Span<byte> bytes = stackalloc byte[8];
        return value is not null && Convert.TryFromBase64String(value, bytes, out var length) && length == 8;
    }
}

public sealed class AssignMessageHandler(IORPStore store, IUserAuthorizationQueries accessService,
    IValidator<AssignMessageRequest> validator, ICurrentUser user, ICorrelationContext correlation,
    AssignmentCoordinator assignments, ITransactionExecutor transactions, MessageAuthorizationService authorization)
{
    public Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
        => transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, ct);
            await validator.ValidateAndThrowAsync(request, ct);
            var message = access.Message;
            if (!message.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(request.ExpectedVersion)))
                throw new DomainRuleViolationException("The message version has changed. Reload before assigning.");
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
