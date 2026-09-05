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
        join source in db.SwiftMessageSource.AsNoTracking() on message.Id equals source.MessageId
        join activeReview in db.Reviews.AsNoTracking().Where(review => review.Status == ReviewStatus.InProgress)
            on message.Id equals activeReview.MessageId into activeReviews
        from activeReview in activeReviews.DefaultIfEmpty()
        select new MessageReadRow
        {
            Id = message.Id,
            ExternalId = source.ExternalId,
            MessageType = source.MessageType,
            BranchId = source.BranchId,
            DepartmentId = source.DepartmentId,
            State = message.State,
            ReceivedAt = source.ReceivedAt,
            CurrentAssigneeId = message.CurrentAssigneeId,
            ActiveReviewId = activeReview == null ? null : activeReview.Id,
            ActiveReviewLevel = activeReview == null ? null : activeReview.Level,
            ActiveReviewerId = activeReview == null ? null : activeReview.ReviewerId,
            Sender = source.Sender,
            Receiver = source.Receiver,
            Account = source.Account,
            Currency = source.Currency,
            Amount = source.Amount,
            Reference = source.Reference
        };
}
