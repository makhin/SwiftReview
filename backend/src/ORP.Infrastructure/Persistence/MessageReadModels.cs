using Microsoft.EntityFrameworkCore;
using ORP.Domain.Reviews;

namespace ORP.Infrastructure.Persistence;

internal sealed class MessageReadRow
{
    public long Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public string MessageType { get; init; } = null!;
    public int BranchId { get; init; }
    public int DepartmentId { get; init; }
    public Domain.Messages.MessageState State { get; init; }
    public int WorkflowDefinitionId { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public int? CurrentAssigneeId { get; init; }
    public long? ActiveReviewId { get; init; }
    public int? ActiveReviewLevel { get; init; }
    public int? ActiveReviewerId { get; init; }
    public string Sender { get; init; } = null!;
    public string Receiver { get; init; } = null!;
    public string? Account { get; init; }
    public string? Currency { get; init; }
    public decimal? Amount { get; init; }
    public string? Reference { get; init; }
}

internal static class MessageReadModels
{
    public static IQueryable<MessageReadRow> ReadMessages(this ORPDbContext db) =>
        from message in db.Messages.AsNoTracking()
        join source in db.SwiftMessages.AsNoTracking() on message.Id equals source.MessageId
        let firstEntry = db.SwiftMessageEntries.AsNoTracking()
            .Where(entry => entry.MessageId == source.MessageId && entry.Position == 0)
            .FirstOrDefault()
        join activeReview in db.Reviews.AsNoTracking().Where(review => review.Status == ReviewStatus.InProgress)
            on message.Id equals activeReview.MessageId into activeReviews
        from activeReview in activeReviews.DefaultIfEmpty()
        select new MessageReadRow
        {
            Id = message.Id,
            ExternalId = source.WarehouseId,
            MessageType = source.MessageType,
            BranchId = source.BranchId!.Value,
            DepartmentId = source.DepartmentId!.Value,
            State = message.State,
            WorkflowDefinitionId = message.WorkflowDefinitionId,
            ReceivedAt = source.MessageDate ?? source.LoadedAtUtc,
            CurrentAssigneeId = message.CurrentAssigneeId,
            ActiveReviewId = activeReview == null ? null : activeReview.Id,
            ActiveReviewLevel = activeReview == null ? null : activeReview.Level,
            ActiveReviewerId = activeReview == null ? null : activeReview.ReviewerId,
            Sender = source.SenderRequestor ?? string.Empty,
            Receiver = source.ReceiverResponder ?? string.Empty,
            Account = firstEntry == null ? null : firstEntry.Account,
            Currency = firstEntry == null ? null : firstEntry.Currency,
            Amount = firstEntry == null ? null : firstEntry.Amount,
            Reference = firstEntry == null ? null : firstEntry.SenderMessageReference
        };
}
