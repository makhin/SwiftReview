namespace SwiftReview.Domain.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage() { }
    public OutboxMessage(string type, string payloadJson, DateTimeOffset occurredAt, string correlationId)
    { Type = type; PayloadJson = payloadJson; OccurredAt = occurredAt; CorrelationId = correlationId; }
    public long Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public Guid? LockId { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public string CorrelationId { get; private set; } = null!;

    public bool TryLock(DateTimeOffset now, TimeSpan duration, Guid lockId)
    {
        if (ProcessedAt is not null || LockedUntil > now || NextAttemptAt > now) return false;
        LockId = lockId; LockedUntil = now.Add(duration); Attempts++; return true;
    }
    public void MarkProcessed(DateTimeOffset now, Guid lockId)
    {
        EnsureOwner(lockId); ProcessedAt = now; LockedUntil = null; LockId = null; NextAttemptAt = null; LastError = null;
    }
    public void MarkFailed(string error, DateTimeOffset nextAttemptAt, Guid lockId)
    {
        EnsureOwner(lockId); LastError = error; LockedUntil = null; LockId = null; NextAttemptAt = nextAttemptAt;
    }

    private void EnsureOwner(Guid lockId)
    {
        if (LockId != lockId) throw new InvalidOperationException("The outbox lease is no longer owned by this worker.");
    }
}
