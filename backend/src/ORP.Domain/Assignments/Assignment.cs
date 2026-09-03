using ORP.Domain.Common;

namespace ORP.Domain.Assignments;

public sealed class Assignment
{
    private Assignment() { }
    public Assignment(long messageId, int assignedBy, int assignedTo, DateTimeOffset createdAt)
    {
        if (assignedBy == assignedTo) throw new DomainRuleViolationException("A user cannot assign a message to themselves.");
        MessageId = messageId;
        AssignedBy = assignedBy;
        AssignedTo = assignedTo;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }
    public long MessageId { get; private set; }
    public int AssignedBy { get; private set; }
    public int AssignedTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    public void End(DateTimeOffset now)
    {
        if (EndedAt is not null) throw new DomainRuleViolationException("Assignment has already ended.");
        EndedAt = now;
    }
}
