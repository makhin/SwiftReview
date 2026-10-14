using ORP.Domain.Common;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Domain.Messages;

public sealed class Message
{
    private Message() { }

    public Message(long messageId, int workflowDefinitionId)
    {
        if (messageId <= 0) throw new ArgumentOutOfRangeException(nameof(messageId));
        Id = messageId;
        WorkflowDefinitionId = workflowDefinitionId;
        State = MessageState.New;
    }

    public long Id { get; private set; }
    public MessageState State { get; private set; }
    public int? CurrentAssigneeId { get; private set; }
    public int WorkflowDefinitionId { get; private set; }

    public void Assign(int assigneeId)
    {
        State = MessageStateTransitions.Assign(State);
        CurrentAssigneeId = assigneeId;
    }

    public void ChangeWorkflow(WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews)
    {
        if (State is not (MessageState.New or MessageState.Assigned) ||
            reviews.Any(review => review.Status is not (ReviewStatus.Cancelled or ReviewStatus.Undone)))
            throw new DomainRuleViolationException("Workflow can only be changed when there are no reviews or all review attempts have been cancelled or undone.");
        if (!workflow.IsActive) throw new DomainRuleViolationException("The selected workflow is inactive.");
        _ = workflow.RequiredLevels();
        WorkflowDefinitionId = workflow.Id;
    }

    public void Unassign()
    {
        if (CurrentAssigneeId is null)
            throw new DomainRuleViolationException("The message is not assigned.");
        if (State is not (MessageState.Assigned or MessageState.WaitingForSecondReview or MessageState.WaitingForThirdReview or
            MessageState.Completed or MessageState.Rejected))
            throw new DomainRuleViolationException($"The assignment cannot be ended while message is in state '{State}'.");
        CurrentAssigneeId = null;
    }

    public Review StartReview(int level, int reviewerId, WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews,
        DateTimeOffset now, bool preventReviewerReuse = true, bool isGlobalAdministrator = false)
    {
        EnsureWorkflow(workflow);
        if (!isGlobalAdministrator && CurrentAssigneeId != reviewerId)
            throw new DomainRuleViolationException("Only the assigned reviewer can start the review.");
        var expected = MessageReviewRules.ExpectedLevel(workflow, reviews);
        if (expected != level) throw new DomainRuleViolationException($"Review level {level} is not currently active.");
        if (reviews.Any(x => x.Level == level && x.Status is not (ReviewStatus.Undone or ReviewStatus.Cancelled)))
            throw new DomainRuleViolationException("This review level has already been started.");
        if (!isGlobalAdministrator && preventReviewerReuse && reviews.Any(x => x.ReviewerId == reviewerId && x.Status == ReviewStatus.Approved))
            throw new DomainRuleViolationException("Four-eyes principle: a reviewer cannot be reused.");

        State = MessageStateTransitions.StartReview(State, level);
        return new Review(Id, level, reviewerId, now);
    }

    public void Approve(Review review, WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews,
        int actorId, string? comment, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        EnsureWorkflow(workflow);
        if (State == MessageState.Completed) throw new DomainRuleViolationException("A completed message cannot be approved.");
        if (review.MessageId != Id || !reviews.Contains(review))
            throw new DomainRuleViolationException("Review does not belong to this message workflow.");
        if (!isGlobalAdministrator && review.ReviewerId != actorId)
            throw new DomainRuleViolationException("Only the reviewer who started the review can approve it.");
        var next = MessageReviewRules.NextLevelAfterApproval(workflow, reviews, review);
        var target = MessageStateTransitions.Approve(State, review.Level, next);
        review.Approve(comment, now);
        State = target;
    }

    public void Reject(Review review, int actorId, string? comment, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        if (review.MessageId != Id) throw new DomainRuleViolationException("Review does not belong to this message.");
        if (!isGlobalAdministrator && review.ReviewerId != actorId)
            throw new DomainRuleViolationException("Only the reviewer who started the review can reject it.");
        var target = MessageStateTransitions.Reject(State, review.Level);
        review.Reject(comment, now);
        State = target;
    }

    public void CancelReview(Review review, int actorId, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        if (review.MessageId != Id) throw new DomainRuleViolationException("Review does not belong to this message.");
        if (!isGlobalAdministrator && review.ReviewerId != actorId)
            throw new DomainRuleViolationException("Only the reviewer who started the review can cancel it.");
        var target = MessageStateTransitions.CancelReview(State, review.Level);
        review.Cancel(now);
        State = target;
    }

    public Review? UndoLastApproval(Review review, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, int actorId, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        EnsureWorkflow(workflow);
        if (review.MessageId != Id || !reviews.Contains(review)) throw new DomainRuleViolationException("Review does not belong to this message.");
        var decision = MessageReviewRules.CheckUndo(workflow, reviews, review, actorId, isGlobalAdministrator);
        if (decision.ActiveReview is not null && decision.ActiveReview.MessageId != Id)
            throw new DomainRuleViolationException("Only the active next workflow step can be cancelled by undo.");
        var target = MessageStateTransitions.Undo(State, review.Level, decision.NextLevel, decision.ActiveReview?.Level);
        decision.ActiveReview?.Cancel(now);
        review.Undo(now);
        State = target;
        return decision.ActiveReview;
    }

    private void EnsureWorkflow(WorkflowDefinition workflow)
    {
        if (!workflow.IsActive || workflow.Id != WorkflowDefinitionId)
            throw new DomainRuleViolationException("The configured workflow is not active for this message.");
    }
}
