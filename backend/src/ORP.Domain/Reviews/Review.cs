using ORP.Domain.Common;

namespace ORP.Domain.Reviews;

public enum ReviewStatus { InProgress, Approved, Rejected, Undone }

public sealed class Review
{
    private Review() { }

    public Review(long messageId, int level, int reviewerId, DateTimeOffset startedAt)
    {
        if (level is < 1 or > 3) throw new DomainRuleViolationException("Review level must be between 1 and 3.");
        MessageId = messageId;
        Level = level;
        ReviewerId = reviewerId;
        Status = ReviewStatus.InProgress;
        StartedAt = startedAt;
    }

    public long Id { get; private set; }
    public long MessageId { get; private set; }
    public int Level { get; private set; }
    public int ReviewerId { get; private set; }
    public ReviewStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Comment { get; private set; }

    public void Approve(string? comment, DateTimeOffset now)
    {
        EnsureInProgress();
        Status = ReviewStatus.Approved;
        Comment = comment;
        CompletedAt = now;
    }

    public void Reject(string comment, DateTimeOffset now)
    {
        EnsureInProgress();
        if (string.IsNullOrWhiteSpace(comment)) throw new DomainRuleViolationException("A rejection comment is required.");
        Status = ReviewStatus.Rejected;
        Comment = comment;
        CompletedAt = now;
    }

    public void Undo(DateTimeOffset now)
    {
        if (Status != ReviewStatus.Approved) throw new DomainRuleViolationException("Only an approved review can be undone.");
        Status = ReviewStatus.Undone;
        CompletedAt = now;
    }

    private void EnsureInProgress()
    {
        if (Status != ReviewStatus.InProgress) throw new DomainRuleViolationException("A review cannot be completed twice.");
    }
}
