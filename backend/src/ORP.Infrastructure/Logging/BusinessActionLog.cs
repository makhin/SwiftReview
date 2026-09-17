using Microsoft.Extensions.Logging;
using ORP.Domain.Auditing;
using ORP.Infrastructure.Persistence;

namespace ORP.Infrastructure.Logging;

public sealed class BusinessActionLog(ILogger<ORPStore> logger)
{
    private readonly List<AuditEvent> pending = [];

    public void Add(IEnumerable<AuditEvent> events) => pending.AddRange(events);
    public void Clear() => pending.Clear();

    public void Committed()
    {
        foreach (var auditEvent in pending)
            InfrastructureLog.BusinessActionCommitted(logger, auditEvent.EventType, auditEvent.Id,
                auditEvent.MessageId, auditEvent.UserId, auditEvent.OldState, auditEvent.NewState,
                auditEvent.CorrelationId);
        pending.Clear();
    }
}
