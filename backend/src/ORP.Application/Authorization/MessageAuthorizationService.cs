using Microsoft.Extensions.Logging;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Application.Authorization;

public enum MessageActionOwnership { None, Assignee, ActiveReviewer, AssignedReviewer }

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
        return await RequireAsync(new AuthorizedMessage(message, source, reviews, false), permissionName, ct,
            reviewLevel, ownershipRequirement);
    }

    // Reuse a context only within the same transaction attempt; permission reads remain fresh.
    public async Task<AuthorizedMessage> RequireAsync(AuthorizedMessage context, string permissionName, CancellationToken ct,
        int? reviewLevel = null, MessageActionOwnership ownershipRequirement = MessageActionOwnership.None)
    {
        var (message, source, reviews, _) = context;
        var viewOnlyMutation = message.State == MessageState.ViewOnly && permissionName != Permissions.MessageView;
        var currentId = current.UserId;
        var access = await users.CheckAsync(currentId, source.BranchId,
            (viewOnlyMutation || permissionName == Permissions.MessageView) ? source.DepartmentIds : message.ProcessingDepartmentIds, permissionName, ct) ?? throw new UnauthorizedAccessException();
        if (viewOnlyMutation)
        {
            if (!access.IsGlobalAdministrator && !access.CanView)
                throw new UnauthorizedAccessException("The current user is not allowed to perform this action.");
            throw new ORP.Domain.Common.DomainRuleViolationException("View-only messages cannot be changed.");
        }
        if (ownershipRequirement is MessageActionOwnership.Assignee or MessageActionOwnership.AssignedReviewer &&
            (message.CurrentAssigneeId != currentId ||
             (ownershipRequirement == MessageActionOwnership.AssignedReviewer &&
              !reviews.Any(x => x.Status == ReviewStatus.InProgress && x.Level == reviewLevel && x.ReviewerId == currentId))))
            throw new UnauthorizedAccessException("Only the assigned reviewer can confirm the message.");
        if (access.IsGlobalAdministrator) return new AuthorizedMessage(message, source, reviews, access.IsGlobalAdministrator);
        var ownership = ownershipRequirement switch
        {
            MessageActionOwnership.ActiveReviewer => reviews.Any(x =>
                x.Status == ReviewStatus.InProgress && x.Level == reviewLevel &&
                x.ReviewerId == currentId),
            _ => true
        };

        if (access.HasPermission && access.CanView && ownership)
            return new AuthorizedMessage(message, source, reviews, access.IsGlobalAdministrator);

        AuthorizationLog.MessageAuthorizationDenied(logger, message.Id, currentId, permissionName, reviewLevel,
            ownershipRequirement.ToString(), access.HasPermission, access.CanView, ownership,
            correlation.CorrelationId);
        throw new UnauthorizedAccessException("The current user is not allowed to perform this action.");
    }

}
