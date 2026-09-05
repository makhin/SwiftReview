using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Audit;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Application.Assignments.Automatic;

public sealed class AssignmentCoordinator(IORPStore store, IClock clock)
{
    public async Task AssignAsync(Message message, int assignedTo, int? assignedBy, string correlationId,
        CancellationToken cancellationToken)
    {
        var oldState = message.State;
        var previousAssigneeId = message.CurrentAssigneeId;
        var previous = await store.GetActiveAssignmentAsync(message.Id, cancellationToken);
        var now = clock.UtcNow;
        previous?.End(now);
        message.Assign(assignedTo);
        store.AddAssignment(new Assignment(message.Id, assignedBy, assignedTo, now));
        store.AddAudit(AuditEventFactory.Create(message.Id,
            previousAssigneeId is null ? AuditEventType.MessageAssigned : AuditEventType.MessageReassigned,
            assignedBy, now, oldState, message.State,
            new AuditEventDetailsDto(PreviousAssigneeId: previousAssigneeId, AssigneeId: assignedTo),
            correlationId));
    }
}

public sealed class AutomaticAssignmentService(IAutomaticAssignmentQueries queries,
    AssignmentCoordinator assignments)
{
    public async Task<bool> TryAssignAsync(Message message, MessageSourceDto source, int reviewLevel,
        IReadOnlyCollection<Review> reviews, string correlationId, CancellationToken cancellationToken)
    {
        var excluded = ReviewAssignmentRules.ApprovedReviewerIds(reviews);
        var assigneeId = await queries.SelectAssigneeAsync(message.Id, source.BranchId,
            source.DepartmentId, reviewLevel, excluded, cancellationToken);
        if (assigneeId is null) return false;
        if (message.CurrentAssigneeId != assigneeId)
            await assignments.AssignAsync(message, assigneeId.Value, null, correlationId, cancellationToken);
        return true;
    }

    public static DomainRuleViolationException NoCandidate(int level) =>
        new($"No eligible reviewer is available for review level {level}.");
}

public sealed class AssignNewMessageHandler(IORPStore store, AutomaticAssignmentService automaticAssignments)
{
    public async Task<bool> HandleAsync(long messageId, string correlationId,
        CancellationToken cancellationToken)
    {
        var message = await store.FindMessageAsync(messageId, cancellationToken);
        if (message is null || message.State != MessageState.New || message.CurrentAssigneeId is not null)
            return false;
        var source = await store.FindMessageSourceAsync(messageId, cancellationToken)
            ?? throw new ResourceNotFoundException("SWIFT message was not found.");
        var workflow = await store.FindWorkflowAsync(message.WorkflowDefinitionId, cancellationToken)
            ?? throw new ResourceNotFoundException("Workflow was not found.");
        if (!workflow.IsActive) return false;
        var level = workflow.RequiredLevels()[0];
        var reviews = await store.GetReviewsAsync(messageId, cancellationToken);
        if (!await automaticAssignments.TryAssignAsync(message, source, level, reviews, correlationId,
                cancellationToken))
            return false;
        await store.SaveChangesAsync(cancellationToken);
        return true;
    }
}
