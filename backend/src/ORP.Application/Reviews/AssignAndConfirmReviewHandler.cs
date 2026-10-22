using ORP.Domain.Messages;
using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Assignments.Assign;
using ORP.Application.Authorization;
using ORP.Domain.Common;
using ORP.Domain.Identity;

namespace ORP.Application.Reviews;

public sealed class AssignAndConfirmReviewHandler(IValidator<ConfirmReviewRequest> validator,
    ICurrentUser user, MessageAuthorizationService authorization, AssignMessageHandler assign,
    ConfirmReviewHandler confirm, ITransactionExecutor transactions, IORPStore store)
{
    public async Task HandleAsync(long messageId, ConfirmReviewRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await transactions.ExecuteAsync(async ct =>
        {
            var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, ct);
            await authorization.RequireAsync(access, ReviewAssignmentRules.PermissionForLevel(request.Level), ct);
            if (access.Message.State.AssignableReviewLevel() != request.Level)
                throw new DomainRuleViolationException("Only the current workflow level can be assigned and confirmed.");
            if (access.Message.CurrentAssigneeId is int assignee && assignee != user.UserId)
                throw new DomainRuleViolationException("The message is assigned to another reviewer.");
            var workflow = await ReviewHandlerHelper.LoadWorkflowAsync(store, access.Message.WorkflowDefinitionId, ct);
            var activeAssignments = await store.GetActiveAssignmentsAsync(messageId, ct);
            if (access.Message.CurrentAssigneeId is null)
                await assign.ApplyAsync(access, workflow, activeAssignments, new(user.UserId, request.Level), ct);
            await confirm.ApplyAsync(access, workflow, activeAssignments, request, ct);
        }, cancellationToken);
    }
}
