namespace ORP.Domain.Auditing;

public sealed class AccessAuditEvent
{
    public long Id { get; private set; }
    public int ActorId { get; init; }
    public int? TargetUserId { get; init; }
    public int? RoleId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string BeforeJson { get; init; } = null!;
    public string AfterJson { get; init; } = null!;
    public string CorrelationId { get; init; } = null!;
}
