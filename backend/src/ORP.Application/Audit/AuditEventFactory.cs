using System.Text.Json;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Application.Audit;

internal static class AuditEventFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AuditEvent Create(long messageId, AuditEventType eventType, int? userId,
        DateTimeOffset timestamp, MessageState? oldState, MessageState? newState,
        AuditEventDetailsDto details, string correlationId, Review? review = null) =>
        new(messageId, eventType, userId, timestamp, oldState, newState,
            JsonSerializer.Serialize(details, JsonOptions), correlationId, review);
}
