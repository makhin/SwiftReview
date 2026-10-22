using ORP.Domain.Common;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Domain.Messages;

public sealed class Message
{
    private readonly List<MessageDepartment> _departments = [];
    private Message() { }

    public Message(long messageId, int workflowDefinitionId)
    {
        if (messageId <= 0) throw new ArgumentOutOfRangeException(nameof(messageId));
        Id = messageId;
        WorkflowDefinitionId = workflowDefinitionId;
        State = MessageState.New;
    }

    public Message(long messageId, int workflowDefinitionId, int branchId, IReadOnlyCollection<int> departmentIds, DateTimeOffset receivedAt)
        : this(messageId, workflowDefinitionId)
    {
        if (workflowDefinitionId <= 0) throw new ArgumentOutOfRangeException(nameof(workflowDefinitionId));
        if (branchId <= 0) throw new ArgumentOutOfRangeException(nameof(branchId));
        if (departmentIds is null || departmentIds.Count == 0 || departmentIds.Any(id => id <= 0))
            throw new ArgumentException("At least one positive department ID is required.", nameof(departmentIds));
        BranchId = branchId;
        _departments.AddRange(departmentIds.Distinct().Order().Select(id => new MessageDepartment(messageId, id)));
        ReceivedAt = receivedAt;
    }

    public Message(long messageId, int workflowDefinitionId, int branchId, int departmentId, DateTimeOffset receivedAt)
        : this(messageId, workflowDefinitionId, branchId, [departmentId], receivedAt) { }

    public long Id { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public MessageState State { get; private set; }
    public int? CurrentAssigneeId { get; private set; }
    public int WorkflowDefinitionId { get; private set; }
    public IReadOnlyList<int> ProcessingDepartmentIds => _departments.Where(x => x.CanProcess).Select(x => x.DepartmentId).Order().ToArray();
    public int? BranchId { get; private set; }
    public IReadOnlyCollection<MessageDepartment> Departments => _departments;
    public IReadOnlyList<int> DepartmentIds => _departments.Select(x => x.DepartmentId).Order().ToArray();
    public DateTimeOffset? ReceivedAt { get; private set; }

    public void ApplyRouting(WorkflowDefinition workflow, IReadOnlyCollection<int> processingDepartmentIds)
    {
        if (State is not (MessageState.New or MessageState.Assigned))
            throw new DomainRuleViolationException("Routing can only be applied before processing starts.");
        if (!workflow.IsActive || processingDepartmentIds.Except(DepartmentIds).Any())
            throw new DomainRuleViolationException("Invalid workflow processing scope.");
        _ = workflow.RequiredLevels();
        var viewOnly = workflow.ConfirmationMode == ConfirmationMode.None;
        if (viewOnly != (processingDepartmentIds.Count == 0) || (viewOnly && CurrentAssigneeId is not null))
            throw new DomainRuleViolationException("The processing scope must match the workflow confirmation policy.");
        WorkflowDefinitionId = workflow.Id;
        foreach (var department in _departments)
            department.SetProcessing(processingDepartmentIds.Contains(department.DepartmentId));
        if (viewOnly) State = MessageState.ViewOnly;
    }

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
        DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        ValidateReviewStart(level, reviewerId, workflow, reviews, isGlobalAdministrator);
        State = MessageStateTransitions.StartReview(State, level);
        return new Review(Id, level, reviewerId, now);
    }

    public void ValidateReviewStart(int level, int reviewerId, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, bool isGlobalAdministrator = false)
    {
        if (GetReviewStartError(level, reviewerId, workflow, reviews, isGlobalAdministrator) is { } error)
            throw new DomainRuleViolationException(error);
    }

    public string? GetReviewStartError(int level, int reviewerId, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, bool isGlobalAdministrator = false)
    {
        if (WorkflowError(workflow) is { } workflowError) return workflowError;
        if (workflow.ConfirmationMode == ConfirmationMode.None) return "This workflow has no confirmations.";
        if (workflow.ConfirmationMode == ConfirmationMode.Immediate && CurrentAssigneeId is null)
            return "Assign the message before confirming it.";
        if (CurrentAssigneeId != reviewerId) return "Only the assigned reviewer can start the review.";
        if (MessageReviewRules.ExpectedLevel(workflow, reviews) != level)
            return $"Review level {level} is not currently active.";
        if (reviews.Any(x => x.Level == level && x.Status is not (ReviewStatus.Undone or ReviewStatus.Cancelled)))
            return "This review level has already been started.";
        if (!ReviewerAllowed(reviewerId, workflow, reviews, isGlobalAdministrator))
            return "Four-eyes principle: a reviewer cannot be reused.";
        return State != MessageState.New && State.AssignableReviewLevel() == level
            ? null : $"Action 'StartReview' is not allowed while message is in state '{State}'.";
    }

    // Session actions retain their existing participation restrictions, including administrator overrides.
    public bool CanParticipateInReview(int level, int reviewerId, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, bool isGlobalAdministrator) => isGlobalAdministrator ||
        (State != MessageState.New && State.ReviewLevel() == level && ReviewerAllowed(reviewerId, workflow, reviews, false));

    public bool CanResumeReview(int level, int reviewerId, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, bool isGlobalAdministrator = false) =>
        WorkflowError(workflow) is null && workflow.ConfirmationMode == ConfirmationMode.ReviewSession &&
        State.IsReviewInProgress() && State.ReviewLevel() == level && CurrentAssigneeId == reviewerId &&
        reviews.Any(r => r.Status == ReviewStatus.InProgress && r.Level == level && r.ReviewerId == reviewerId) &&
        ReviewerAllowed(reviewerId, workflow, reviews, isGlobalAdministrator);

    private static bool ReviewerAllowed(int reviewerId, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, bool isGlobalAdministrator) =>
        isGlobalAdministrator || workflow.AllowReviewerReuse ||
        !reviews.Any(x => x.ReviewerId == reviewerId && x.Status == ReviewStatus.Approved);

    public void Approve(Review review, WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews,
        int actorId, string? comment, DateTimeOffset now)
    {
        EnsureWorkflow(workflow);
        if (State == MessageState.Completed) throw new DomainRuleViolationException("A completed message cannot be approved.");
        if (review.MessageId != Id || !reviews.Contains(review))
            throw new DomainRuleViolationException("Review does not belong to this message workflow.");
        if (CurrentAssigneeId != actorId || review.ReviewerId != actorId)
            throw new DomainRuleViolationException("Only the assigned reviewer who started the review can approve it.");
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
        if (GetUndoError(review, workflow, reviews, actorId, isGlobalAdministrator) is { } error)
            throw new DomainRuleViolationException(error);
        var activeReview = reviews.SingleOrDefault(x => x.Status == ReviewStatus.InProgress);
        var nextLevel = workflow.RequiredLevels().FirstOrDefault(level => level > review.Level);
        var target = MessageStateTransitions.Undo(State, review.Level, nextLevel, activeReview?.Level);
        activeReview?.Cancel(now);
        review.Undo(now);
        State = target;
        return activeReview;
    }

    public string? GetUndoError(Review review, WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews,
        int actorId, bool isGlobalAdministrator = false)
    {
        if (WorkflowError(workflow) is { } workflowError) return workflowError;
        if (review.MessageId != Id || !reviews.Contains(review)) return "Review does not belong to this message.";
        if (MessageReviewRules.UndoError(workflow, reviews, review, actorId, isGlobalAdministrator) is { } policyError)
            return policyError;
        var active = reviews.SingleOrDefault(x => x.Status == ReviewStatus.InProgress);
        if (active is not null && active.MessageId != Id)
            return "Only the active next workflow step can be cancelled by undo.";
        var nextLevel = workflow.RequiredLevels().FirstOrDefault(level => level > review.Level);
        return MessageStateTransitions.UndoError(State, nextLevel, active?.Level);
    }

    private string? WorkflowError(WorkflowDefinition workflow) =>
        !workflow.IsActive || workflow.Id != WorkflowDefinitionId
            ? "The configured workflow is not active for this message." : null;

    private void EnsureWorkflow(WorkflowDefinition workflow)
    {
        if (WorkflowError(workflow) is { } error) throw new DomainRuleViolationException(error);
    }
}
