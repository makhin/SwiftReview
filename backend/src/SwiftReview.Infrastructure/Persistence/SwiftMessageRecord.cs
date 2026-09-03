namespace SwiftReview.Infrastructure.Persistence;

// Normalized read-only projection over the SWIFT-owned dbo.Messages table. Its SQL
// definition will be added when the Body-to-ORP mapping rules are known.
public sealed class SwiftMessageRecord
{
    public long MessageId { get; init; }
    public string ExternalId { get; init; } = null!;
    public string MessageType { get; init; } = null!;
    public int BranchId { get; init; }
    public int DepartmentId { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public string Sender { get; init; } = null!;
    public string Receiver { get; init; } = null!;
    public string? Account { get; init; }
    public string? Currency { get; init; }
    public decimal? Amount { get; init; }
    public string? Reference { get; init; }
}
