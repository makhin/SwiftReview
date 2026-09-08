using Microsoft.Extensions.Logging;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;

namespace ORP.Infrastructure.Logging;

internal static partial class InfrastructureLog
{
    [LoggerMessage(EventId = 2000, EventName = "BusinessActionCommitted", Level = LogLevel.Information,
        Message = "Business action {EventType} committed as audit event {AuditEventId} for message {MessageId} by actor {ActorUserId}: {OldState} -> {NewState}; correlation {CorrelationId}")]
    public static partial void BusinessActionCommitted(ILogger logger, AuditEventType eventType,
        long auditEventId, long messageId, int? actorUserId, MessageState? oldState, MessageState? newState,
        string correlationId);
}
