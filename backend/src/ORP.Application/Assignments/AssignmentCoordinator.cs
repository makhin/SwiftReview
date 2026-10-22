using ORP.Application.Abstractions;
using ORP.Application.Audit;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;

namespace ORP.Application.Assignments;

public sealed class AssignmentCoordinator(IORPStore store, IClock clock)
{
    public async Task AssignAsync(Message message, int assignedTo, int assignedBy, string correlationId,
        CancellationToken cancellationToken, int? level = null)
    {
        var targetLevel = level ?? ReviewAssignmentRules.AssignmentLevelForState(message.State)
            ?? throw new DomainRuleViolationException("The message has no assignable level.");
        var oldState = message.State;
        var previous = (await store.GetActiveAssignmentsAsync(message.Id, cancellationToken))
            .SingleOrDefault(a => a.Level == targetLevel && a.EndedAt is null);
        var previousAssigneeId = previous?.AssignedTo;
        var now = clock.UtcNow;
        previous?.End(now);
        if (targetLevel == ReviewAssignmentRules.CurrentLevel(message.State)) message.Assign(assignedTo);
        store.AddAssignment(new Assignment(message.Id, assignedBy, assignedTo, now, targetLevel));
        store.AddAudit(AuditEventFactory.Create(message.Id,
            previousAssigneeId is null ? AuditEventType.MessageAssigned : AuditEventType.MessageReassigned,
            assignedBy, now, oldState, message.State,
            new AuditEventDetailsDto(PreviousAssigneeId: previousAssigneeId, AssigneeId: assignedTo, ReviewLevel: targetLevel),
            correlationId));
    }

    private void End(Message message, Assignment assignment, int actorId, string correlationId)
    {
        assignment.End(clock.UtcNow);
        store.AddAudit(AuditEventFactory.Create(message.Id, AuditEventType.MessageUnassigned,
            actorId, clock.UtcNow, message.State, message.State,
            new AuditEventDetailsDto(PreviousAssigneeId: assignment.AssignedTo, ReviewLevel: assignment.Level), correlationId));
    }

    public async Task UnassignAsync(Message message, int actorId, string correlationId,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in (await store.GetActiveAssignmentsAsync(message.Id, cancellationToken))
                     .Where(a => a.EndedAt is null))
            End(message, assignment, actorId, correlationId);
        if (message.CurrentAssigneeId is not null) message.Unassign();
    }

    public async Task AfterApprovalAsync(Message message, int approvedLevel, WorkflowDefinition workflow,
        int actorId, string correlationId, CancellationToken ct)
    {
        if (message.State == MessageState.Completed)
        {
            await UnassignAsync(message, actorId, correlationId, ct);
            return;
        }
        var active = await store.GetActiveAssignmentsAsync(message.Id, ct);
        var previousAssignee = message.CurrentAssigneeId;
        var completed = active.SingleOrDefault(a => a.Level == approvedLevel && a.EndedAt is null);
        if (completed is not null) End(message, completed, actorId, correlationId);
        if (message.CurrentAssigneeId is not null) message.Unassign();
        var nextLevel = ReviewAssignmentRules.AssignmentLevelForState(message.State)!.Value;
        var next = active.SingleOrDefault(a => a.Level == nextLevel && a.EndedAt is null);
        if (next is not null) message.Assign(next.AssignedTo);
        else if (workflow.AssignmentRetentionMode == AssignmentRetentionMode.UntilWorkflowCompleted && previousAssignee is int retained)
            await AssignAsync(message, retained, actorId, correlationId, ct, nextLevel);
    }

    public async Task AfterUndoAsync(Message message, WorkflowDefinition workflow,
        int actorId, string correlationId, CancellationToken ct)
    {
        var retained = message.CurrentAssigneeId;
        if (retained is not null) message.Unassign();
        var level = ReviewAssignmentRules.AssignmentLevelForState(message.State)!.Value;
        var active = await store.GetActiveAssignmentsAsync(message.Id, ct);
        var assignment = active.SingleOrDefault(a => a.Level == level && a.EndedAt is null);
        if (assignment is not null) message.Assign(assignment.AssignedTo);
        else if (workflow.AssignmentRetentionMode == AssignmentRetentionMode.UntilWorkflowCompleted && retained is int assignee)
            await AssignAsync(message, assignee, actorId, correlationId, ct, level);
    }
}
