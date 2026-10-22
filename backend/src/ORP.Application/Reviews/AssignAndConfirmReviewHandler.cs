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
    ConfirmReviewHandler confirm, ITransactionExecutor transactions)
{
    public Task HandleAsync(long messageId, ConfirmReviewRequest request, CancellationToken cancellationToken) =>
        transactions.ExecuteAsync(async ct =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, ct);
            await authorization.RequireAsync(messageId, MessageAuthorizationService.ReviewPermission(request.Level), ct);
            if (ReviewAssignmentRules.AssignmentLevelForState(access.Message.State) != request.Level)
                throw new DomainRuleViolationException("Only the current workflow level can be assigned and confirmed.");
            if (access.Message.CurrentAssigneeId is int assignee && assignee != user.UserId)
                throw new DomainRuleViolationException("The message is assigned to another reviewer.");
            if (access.Message.CurrentAssigneeId is null)
                await assign.HandleInTransactionAsync(messageId, new(user.UserId, request.Level), ct);
            await confirm.HandleInTransactionAsync(messageId, request, ct);
        }, cancellationToken);
}
