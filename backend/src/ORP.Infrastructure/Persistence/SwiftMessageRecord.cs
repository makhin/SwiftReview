using ORP.Domain.Messages;

namespace ORP.Infrastructure.Persistence;

public sealed class SwiftMessageRecord
{
    public long MessageId { get; init; }
    public string? Branch { get; init; }
    public string? MessageType { get; init; }
    public string? ReferenceId { get; init; }
    public MessageDirection? Direction { get; init; }
    public string? Sender { get; init; }
    public string? Receiver { get; init; }
    public string? Host { get; init; }
    public DateTime? Time { get; init; }
    public string Body { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public string FilePath { get; init; } = null!;
    public string ContentHash { get; init; } = null!;
}
