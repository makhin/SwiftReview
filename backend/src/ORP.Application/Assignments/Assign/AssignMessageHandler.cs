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
        RuleFor(x => x.Level).InclusiveBetween(1, 3).When(x => x.Level.HasValue);
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
        var workflow = await store.FindWorkflowAsync(message.WorkflowDefinitionId, ct)
            ?? throw new ResourceNotFoundException("Workflow was not found.");
        var reviewLevel = ReviewAssignmentRules.RequireAssignableLevel(message, workflow, access.Reviews, request.Level);
        var activeAssignments = await store.GetActiveAssignmentsAsync(message.Id, ct);
        var source = access.Source;
        var target = await accessService.CheckAsync(request.AssignedTo, source.BranchId, message.ProcessingDepartmentIds,
            ReviewAssignmentRules.PermissionForLevel(reviewLevel), ct)
            ?? throw new ResourceNotFoundException("Assignee was not found.");
        var reviews = access.Reviews;
        if (!ReviewAssignmentRules.IsEligible(request.AssignedTo, target,
                ReviewAssignmentRules.ExcludedReviewerIds(workflow, reviews, activeAssignments, reviewLevel),
                activeAssignments.SingleOrDefault(a => a.Level == reviewLevel && a.EndedAt is null)?.AssignedTo ??
                    (reviewLevel == ReviewAssignmentRules.CurrentLevel(message.State) ? message.CurrentAssigneeId : null)))
            throw new ValidationException("The assignee is not eligible to review the message in its current workflow state.");
        await assignments.AssignAsync(message, request.AssignedTo, user.UserId, correlation.CorrelationId,
            ct, reviewLevel);
        await store.SaveChangesAsync(ct);
    }
}
