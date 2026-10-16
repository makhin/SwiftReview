using Microsoft.EntityFrameworkCore;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Infrastructure.Persistence;

internal sealed class MessageReadRow
{
    public byte[] RowVersion { get; init; } = [];
    public long Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public MessageDirection? Direction { get; init; }
    public string MessageType { get; init; } = null!;
    public int BranchId { get; init; }
    public string BranchName { get; init; } = null!;
    public string[] DepartmentNames { get; init; } = [];
    public string? CurrentAssigneeName { get; init; }
    public string? ActiveReviewerName { get; init; }
    public string WorkflowDefinitionName { get; init; } = null!;
    public int[] DepartmentIds { get; init; } = [];
    public Domain.Messages.MessageState State { get; init; }
    public int WorkflowDefinitionId { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public int? CurrentAssigneeId { get; init; }
    public long? ActiveReviewId { get; init; }
    public int? ActiveReviewLevel { get; init; }
    public int? ActiveReviewerId { get; init; }
    public string Sender { get; init; } = null!;
    public string Receiver { get; init; } = null!;
}

internal static class MessageReadModels
{
    public static Task<Dictionary<MessageState, int>> CountByStateAsync(this IQueryable<MessageReadRow> query,
        CancellationToken ct) => query.GroupBy(x => x.State)
        .Select(g => new { State = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.State, x => x.Count, ct);

    public static IQueryable<MessageReadRow> ReadAccessibleMessages(this ORPDbContext db, int userId,
        string permission = Domain.Identity.Permissions.MessageView) => db.ReadMessages(userId).Where(message =>
        db.Users.Any(user => user.Id == userId && user.IsGlobalAdministrator) ||
        db.UserRoles.Any(role => role.UserId == userId && role.BranchId == message.BranchId &&
            db.MessageDepartments.Any(department => department.MessageId == message.Id &&
                department.DepartmentId == role.DepartmentId) &&
            db.UserRoles.Any(view => view.UserId == userId && view.BranchId == message.BranchId &&
                view.DepartmentId == role.DepartmentId && view.Role.Permissions.Any(grant =>
                    grant.Permission.Name == Domain.Identity.Permissions.MessageView)) &&
            role.Role.Permissions.Any(grant => grant.Permission.Name == permission)));

    public static IQueryable<MessageReadRow> ReadMessages(this ORPDbContext db, int? visibilityUserId = null) =>
        from message in db.Messages.AsNoTracking()
        join source in db.SwiftMessages.AsNoTracking() on message.Id equals source.MessageId
        join activeReview in db.Reviews.AsNoTracking().Where(review => review.Status == ReviewStatus.InProgress)
            on message.Id equals activeReview.MessageId into activeReviews
        from activeReview in activeReviews.DefaultIfEmpty()
        let visibleDepartments = message.Departments.Where(department => visibilityUserId == null ||
            db.Users.Any(user => user.Id == visibilityUserId && user.IsGlobalAdministrator) ||
            db.UserRoles.Any(role => role.UserId == visibilityUserId && role.BranchId == message.BranchId &&
                role.DepartmentId == department.DepartmentId && role.Role.Permissions.Any(grant =>
                    grant.Permission.Name == Domain.Identity.Permissions.MessageView)))
        select new MessageReadRow
        {
            Id = message.Id,
            RowVersion = message.RowVersion,
            ExternalId = source.ReferenceId ?? source.FileName,
            MessageType = source.MessageType!,
            Direction = source.Direction,
            BranchId = message.BranchId!.Value,
            BranchName = db.Branches.Where(branch => branch.Id == message.BranchId).Select(branch => branch.Name).First(),
            DepartmentNames = visibleDepartments.OrderBy(x => x.DepartmentId)
                .Select(x => db.Departments.Where(department => department.Id == x.DepartmentId)
                    .Select(department => department.Name).First()).ToArray(),
            CurrentAssigneeName = db.Users.Where(user => user.Id == message.CurrentAssigneeId)
                .Select(user => user.DisplayName).FirstOrDefault(),
            ActiveReviewerName = db.Users.Where(user => activeReview != null && user.Id == activeReview.ReviewerId)
                .Select(user => user.DisplayName).FirstOrDefault(),
            WorkflowDefinitionName = db.WorkflowDefinitions.Where(workflow => workflow.Id == message.WorkflowDefinitionId)
                .Select(workflow => workflow.Name).First(),
            DepartmentIds = visibleDepartments.OrderBy(x => x.DepartmentId).Select(x => x.DepartmentId).ToArray(),
            State = message.State,
            WorkflowDefinitionId = message.WorkflowDefinitionId,
            ReceivedAt = message.ReceivedAt!.Value,
            CurrentAssigneeId = message.CurrentAssigneeId,
            ActiveReviewId = activeReview == null ? null : activeReview.Id,
            ActiveReviewLevel = activeReview == null ? null : activeReview.Level,
            ActiveReviewerId = activeReview == null ? null : activeReview.ReviewerId,
            Sender = source.Sender ?? string.Empty,
            Receiver = source.Receiver ?? string.Empty
        };
}
