using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Domain.Auditing;

public enum AuditEventType
{
    MessageRegistered,
    MessageAssigned,
    MessageReassigned,
    ReviewStarted,
    ReviewApproved,
    MessageCompleted,
    ReviewRejected,
    ConfirmationUndone
}

public sealed class AuditEvent
{
    private AuditEvent() { }
    public AuditEvent(long messageId, AuditEventType eventType, int? userId, DateTimeOffset timestamp,
        MessageState? oldState, MessageState? newState, string detailsJson, string correlationId, Review? review = null)
    {
        MessageId = messageId; EventType = eventType; UserId = userId; Timestamp = timestamp;
        OldState = oldState; NewState = newState; DetailsJson = detailsJson; CorrelationId = correlationId;
        Review = review;
    }
    public AuditEvent(Message message, AuditEventType eventType, int? userId, DateTimeOffset timestamp,
        MessageState? oldState, MessageState? newState, string detailsJson, string correlationId)
        : this(0, eventType, userId, timestamp, oldState, newState, detailsJson, correlationId) => Message = message;
    public long Id { get; private set; }
    public long MessageId { get; private set; }
    public AuditEventType EventType { get; private set; }
    public int? UserId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public MessageState? OldState { get; private set; }
    public MessageState? NewState { get; private set; }
    public string DetailsJson { get; private set; } = null!;
    public string CorrelationId { get; private set; } = null!;
    public long? ReviewId { get; private set; }
    public Message? Message { get; private set; }
    public User? User { get; private set; }
    public Review? Review { get; private set; }
}
