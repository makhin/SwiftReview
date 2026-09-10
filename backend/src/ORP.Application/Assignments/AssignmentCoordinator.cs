using ORP.Application.Abstractions;
using ORP.Application.Audit;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Messages;

namespace ORP.Application.Assignments;

public sealed class AssignmentCoordinator(IORPStore store, IClock clock)
{
    public async Task AssignAsync(Message message, int assignedTo, int assignedBy, string correlationId,
        CancellationToken cancellationToken, bool allowSelfAssignment = false)
    {
        var oldState = message.State;
        var previousAssigneeId = message.CurrentAssigneeId;
        var previous = await store.GetActiveAssignmentAsync(message.Id, cancellationToken);
        var now = clock.UtcNow;
        previous?.End(now);
        message.Assign(assignedTo);
        store.AddAssignment(new Assignment(message.Id, assignedBy, assignedTo, now, allowSelfAssignment));
        store.AddAudit(AuditEventFactory.Create(message.Id,
            previousAssigneeId is null ? AuditEventType.MessageAssigned : AuditEventType.MessageReassigned,
            assignedBy, now, oldState, message.State,
            new AuditEventDetailsDto(PreviousAssigneeId: previousAssigneeId, AssigneeId: assignedTo),
            correlationId));
    }

    public async Task UnassignAsync(Message message, int actorId, string correlationId,
        CancellationToken cancellationToken)
    {
        var previousAssigneeId = message.CurrentAssigneeId;
        var assignment = await store.GetActiveAssignmentAsync(message.Id, cancellationToken)
            ?? throw new DomainRuleViolationException("The message has no active assignment.");
        var now = clock.UtcNow;
        assignment.End(now);
        message.Unassign();
        store.AddAudit(AuditEventFactory.Create(message.Id, AuditEventType.MessageUnassigned,
            actorId, now, message.State, message.State,
            new AuditEventDetailsDto(PreviousAssigneeId: previousAssigneeId), correlationId));
    }
}
