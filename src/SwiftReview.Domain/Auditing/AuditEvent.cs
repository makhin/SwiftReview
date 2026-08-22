using SwiftReview.Domain.Messages;

namespace SwiftReview.Domain.Auditing;

public sealed class AuditEvent
{
    private AuditEvent() { }
    public AuditEvent(long messageId, string eventType, int? userId, DateTimeOffset timestamp,
        string? oldState, string? newState, string detailsJson, string correlationId)
    {
        MessageId = messageId; EventType = eventType; UserId = userId; Timestamp = timestamp;
        OldState = oldState; NewState = newState; DetailsJson = detailsJson; CorrelationId = correlationId;
    }
    public AuditEvent(Message message, string eventType, int? userId, DateTimeOffset timestamp,
        string? oldState, string? newState, string detailsJson, string correlationId)
        : this(0, eventType, userId, timestamp, oldState, newState, detailsJson, correlationId) => Message = message;
    public long Id { get; private set; }
    public long MessageId { get; private set; }
    public string EventType { get; private set; } = null!;
    public int? UserId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string? OldState { get; private set; }
    public string? NewState { get; private set; }
    public string DetailsJson { get; private set; } = null!;
    public string CorrelationId { get; private set; } = null!;
    public Message? Message { get; private set; }
}
