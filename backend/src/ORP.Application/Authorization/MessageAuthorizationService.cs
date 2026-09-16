using Microsoft.Extensions.Logging;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Application.Authorization;

public enum MessageActionOwnership { None, Assignee, ActiveReviewer }

public sealed record AuthorizedMessage(Message Message, MessageSourceDto Source, List<Review> Reviews,
    bool IsGlobalAdministrator);

public sealed class MessageAuthorizationService(IORPStore store, IUserAuthorizationQueries users,
    ICurrentUser current, ICorrelationContext correlation, ILogger<MessageAuthorizationService> logger)
{
    public async Task<AuthorizedMessage> RequireAsync(long messageId, string permissionName, CancellationToken ct,
        int? reviewLevel = null, MessageActionOwnership ownershipRequirement = MessageActionOwnership.None)
    {
        var message = await store.FindMessageAsync(messageId, ct) ?? throw new ResourceNotFoundException("Message not found.");
        var source = await store.FindMessageSourceAsync(messageId, ct) ?? throw new ResourceNotFoundException("SWIFT message not found.");
        var reviews = await store.GetReviewsAsync(messageId, ct);
        var currentId = current.UserId;
        var access = await users.CheckAsync(currentId, source.BranchId, source.DepartmentId, permissionName, ct) ?? throw new UnauthorizedAccessException();
        if (access.IsGlobalAdministrator) return new AuthorizedMessage(message, source, reviews, access.IsGlobalAdministrator);
        var permission = access.HasPermission;
        var branch = access.CanView;
        var department = branch;
        var stateOk = reviewLevel switch
        {
            1 => message.State is MessageState.Assigned or MessageState.FirstReviewInProgress,
            2 => message.State is MessageState.WaitingForSecondReview or MessageState.SecondReviewInProgress,
            3 => message.State is MessageState.WaitingForThirdReview or MessageState.ThirdReviewInProgress,
            _ => true
        };
        var fourEyes = reviewLevel is null || reviews.All(x => x.Status != ReviewStatus.Approved || x.ReviewerId != currentId);
        var ownership = ownershipRequirement switch
        {
            MessageActionOwnership.Assignee => message.CurrentAssigneeId == currentId,
            MessageActionOwnership.ActiveReviewer => reviews.Any(x =>
                x.Status == ReviewStatus.InProgress && x.Level == reviewLevel &&
                x.ReviewerId == currentId),
            _ => true
        };
        if (permissionName != Permissions.ReviewUndo && permission && branch && stateOk && fourEyes && ownership)
            return new AuthorizedMessage(message, source, reviews, access.IsGlobalAdministrator);

        AuthorizationLog.MessageAuthorizationDenied(logger, messageId, currentId, permissionName, reviewLevel,
            ownershipRequirement.ToString(), permission, branch, department, stateOk, fourEyes, ownership,
            correlation.CorrelationId);
        throw new UnauthorizedAccessException("The current user is not allowed to perform this action.");
    }

    public static string ReviewPermission(int level) => level switch
    {
        1 => Permissions.ReviewLevel1, 2 => Permissions.ReviewLevel2, 3 => Permissions.ReviewLevel3, _ => "invalid"
    };
}
