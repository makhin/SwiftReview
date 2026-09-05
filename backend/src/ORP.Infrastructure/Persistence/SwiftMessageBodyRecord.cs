namespace ORP.Infrastructure.Persistence;

public sealed class SwiftMessageBodyRecord
{
    public long MessageId { get; init; }
    public string? Body { get; init; }
}
