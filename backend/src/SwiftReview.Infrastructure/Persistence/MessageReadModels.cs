using Microsoft.EntityFrameworkCore;

namespace SwiftReview.Infrastructure.Persistence;

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
    public string Sender { get; init; } = null!;
    public string Receiver { get; init; } = null!;
    public string? Account { get; init; }
    public string? Currency { get; init; }
    public decimal? Amount { get; init; }
    public string? Reference { get; init; }
}

internal static class MessageReadModels
{
    public static IQueryable<MessageReadRow> ReadMessages(this SwiftReviewDbContext db) =>
        from message in db.Messages.AsNoTracking()
        join source in db.SwiftMessageSource.AsNoTracking() on message.Id equals source.MessageId
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
            Sender = source.Sender,
            Receiver = source.Receiver,
            Account = source.Account,
            Currency = source.Currency,
            Amount = source.Amount,
            Reference = source.Reference
        };
}
