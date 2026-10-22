using ORP.Domain.Assignments;
using ORP.Domain.Workflows;
using ORP.Domain.Messages;
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
    public async Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, ct);
            var workflow = await store.FindWorkflowAsync(access.Message.WorkflowDefinitionId, ct)
                ?? throw new ResourceNotFoundException("Workflow was not found.");
            await ApplyAsync(access, workflow, await store.GetActiveAssignmentsAsync(messageId, ct), request, ct);
        }, cancellationToken);
    }

    // The caller owns validation, authorization and the transaction.
    internal async Task ApplyAsync(AuthorizedMessage access, WorkflowDefinition workflow,
        List<Assignment> activeAssignments, AssignMessageRequest request, CancellationToken ct)
    {
        var message = access.Message;
        var reviewLevel = ReviewAssignmentRules.RequireAssignableLevel(message, workflow, access.Reviews, request.Level);
        var source = access.Source;
        var target = await accessService.CheckAsync(request.AssignedTo, source.BranchId, message.ProcessingDepartmentIds,
            ReviewAssignmentRules.PermissionForLevel(reviewLevel), ct)
            ?? throw new ResourceNotFoundException("Assignee was not found.");
        var reviews = access.Reviews;
        if (!ReviewAssignmentRules.IsEligible(request.AssignedTo, target,
                ReviewAssignmentRules.ExcludedReviewerIds(workflow, reviews, activeAssignments, reviewLevel),
                activeAssignments.SingleOrDefault(a => a.Level == reviewLevel && a.EndedAt is null)?.AssignedTo ??
                    (reviewLevel == message.State.ReviewLevel() ? message.CurrentAssigneeId : null)))
            throw new ValidationException("The assignee is not eligible to review the message in its current workflow state.");
        await assignments.AssignAsync(message, request.AssignedTo, user.UserId, correlation.CorrelationId,
            ct, reviewLevel, activeAssignments);
        await store.SaveChangesAsync(ct);
    }
}
