namespace ORP.Infrastructure.Persistence;

public enum SwiftMessageRoutingStatus
{
    Routed,
    Unroutable,
    Conflict
}

public sealed class SwiftMessageRecord
{
    public long MessageId { get; init; }
    public string WarehouseId { get; init; } = null!;
    public bool BodyContainsMx { get; init; }
    public bool BodyContainsMt { get; init; }
    public string? Json { get; init; }
    public string? Body { get; init; }
    public string? BackendDirection { get; init; }
    public string? CounterParty { get; init; }
    public string? CounterPartyCountry { get; init; }
    public DateTimeOffset? CreationDate { get; init; }
    public string? Direction { get; init; }
    public DateTimeOffset? LastModificationDate { get; init; }
    public DateTimeOffset? MessageDate { get; init; }
    public int MessageLength { get; init; }
    public string? MessageFormatVersion { get; init; }
    public string? MessageInputReference { get; init; }
    public string MessageType { get; init; } = null!;
    public string? MessageTypeShort { get; init; }
    public string? ModifiedBy { get; init; }
    public string? ReceiverResponder { get; init; }
    public string? SenderRequestor { get; init; }
    public string? SequenceNumber { get; init; }
    public string? SessionNumber { get; init; }
    public string? Service { get; init; }
    public string? SourceInterface { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? StatusDate { get; init; }
    public int? BranchId { get; init; }
    public int? DepartmentId { get; init; }
    public SwiftMessageRoutingStatus RoutingStatus { get; init; }
    public string? RoutingError { get; init; }
    public DateTimeOffset LastSynchronizedAtUtc { get; init; }
    public ICollection<SwiftMessageEntryRecord> Entries { get; init; } = [];
}
