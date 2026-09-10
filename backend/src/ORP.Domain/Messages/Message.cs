using Stateless;
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
        var machine = CreateMachine();
        Fire(machine, CurrentAssigneeId is null ? MessageTrigger.Assign : MessageTrigger.Reassign);
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
        var expected = ExpectedLevel(workflow, reviews);
        if (expected != level) throw new DomainRuleViolationException($"Review level {level} is not currently active.");
        if (reviews.Any(x => x.Level == level && x.Status is not (ReviewStatus.Undone or ReviewStatus.Cancelled)))
            throw new DomainRuleViolationException("This review level has already been started.");
        if (!isGlobalAdministrator && preventReviewerReuse && reviews.Any(x => x.ReviewerId == reviewerId && x.Status == ReviewStatus.Approved))
            throw new DomainRuleViolationException("Four-eyes principle: a reviewer cannot be reused.");

        Fire(CreateMachine(), MessageTrigger.StartReview);
        return new Review(Id, level, reviewerId, now);
    }

    public void Approve(Review review, WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews,
        int actorId, string? comment, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        EnsureWorkflow(workflow);
        if (State == MessageState.Completed) throw new DomainRuleViolationException("A completed message cannot be approved.");
        if (!reviews.Contains(review)) throw new DomainRuleViolationException("Review does not belong to this message workflow.");
        if (!isGlobalAdministrator && review.ReviewerId != actorId)
            throw new DomainRuleViolationException("Only the reviewer who started the review can approve it.");
        var required = workflow.RequiredLevels();
        var completed = reviews.Where(x => x.Status == ReviewStatus.Approved).Select(x => x.Level).Append(review.Level).Distinct().ToHashSet();
        var next = required.FirstOrDefault(x => !completed.Contains(x));
        var target = next == 0 ? MessageState.Completed : WaitingState(next);
        var machine = CreateMachine(target);
        EnsureCanFire(machine, MessageTrigger.Approve);
        review.Approve(comment, now);
        machine.Fire(MessageTrigger.Approve);
    }

    public void Reject(Review review, int actorId, string? comment, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        if (review.MessageId != Id) throw new DomainRuleViolationException("Review does not belong to this message.");
        if (!isGlobalAdministrator && review.ReviewerId != actorId)
            throw new DomainRuleViolationException("Only the reviewer who started the review can reject it.");
        var machine = CreateMachine();
        EnsureCanFire(machine, MessageTrigger.Reject);
        review.Reject(comment, now);
        machine.Fire(MessageTrigger.Reject);
    }

    public void CancelReview(Review review, int actorId, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        if (review.MessageId != Id) throw new DomainRuleViolationException("Review does not belong to this message.");
        if (!isGlobalAdministrator && review.ReviewerId != actorId)
            throw new DomainRuleViolationException("Only the reviewer who started the review can cancel it.");
        var activeState = review.Level switch
        {
            1 => MessageState.FirstReviewInProgress,
            2 => MessageState.SecondReviewInProgress,
            3 => MessageState.ThirdReviewInProgress,
            _ => throw new DomainRuleViolationException("Unsupported review level.")
        };
        if (State != activeState) throw new DomainRuleViolationException("This review level is not currently active.");
        var machine = CreateMachine();
        EnsureCanFire(machine, MessageTrigger.CancelReview);
        review.Cancel(now);
        machine.Fire(MessageTrigger.CancelReview);
    }

    public void UndoLastApproval(Review review, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, int actorId, DateTimeOffset now, bool isGlobalAdministrator = false)
    {
        EnsureWorkflow(workflow);
        if (review.MessageId != Id) throw new DomainRuleViolationException("Review does not belong to this message.");
        if (!isGlobalAdministrator && review.ReviewerId != actorId) throw new DomainRuleViolationException("Only the reviewer who approved can undo confirmation.");
        var latestApprovedLevel = workflow.RequiredLevels().LastOrDefault(level =>
            reviews.Any(x => x.Level == level && x.Status == ReviewStatus.Approved));
        if (latestApprovedLevel == 0 || review.Level != latestApprovedLevel)
            throw new DomainRuleViolationException("Only the latest approved workflow step can be undone.");
        var machine = CreateMachine(WaitingBeforeLevel(review.Level));
        EnsureCanFire(machine, MessageTrigger.Undo);
        review.Undo(now);
        machine.Fire(MessageTrigger.Undo);
    }

    private int ExpectedLevel(WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews)
    {
        var approved = reviews.Where(x => x.Status == ReviewStatus.Approved).Select(x => x.Level).ToHashSet();
        return workflow.RequiredLevels().FirstOrDefault(x => !approved.Contains(x));
    }

    private void EnsureWorkflow(WorkflowDefinition workflow)
    {
        if (!workflow.IsActive || workflow.Id != WorkflowDefinitionId)
            throw new DomainRuleViolationException("The configured workflow is not active for this message.");
    }

    private StateMachine<MessageState, MessageTrigger> CreateMachine(MessageState? approveTarget = null)
    {
        var machine = new StateMachine<MessageState, MessageTrigger>(() => State, value => State = value);
        switch (State)
        {
            case MessageState.New:
                machine.Configure(State).Permit(MessageTrigger.Assign, MessageState.Assigned);
                break;
            case MessageState.Assigned:
                machine.Configure(State).PermitReentry(MessageTrigger.Assign)
                    .PermitReentry(MessageTrigger.Reassign).Permit(MessageTrigger.StartReview, MessageState.FirstReviewInProgress);
                break;
            case MessageState.FirstReviewInProgress:
                ConfigureReview(machine, State, approveTarget ?? MessageState.WaitingForSecondReview, MessageState.Assigned);
                break;
            case MessageState.WaitingForSecondReview:
                var second = machine.Configure(State).PermitReentry(MessageTrigger.Assign)
                    .PermitReentry(MessageTrigger.Reassign).Permit(MessageTrigger.StartReview, MessageState.SecondReviewInProgress);
                if (approveTarget is not null) second.Permit(MessageTrigger.Undo, approveTarget.Value);
                break;
            case MessageState.SecondReviewInProgress:
                ConfigureReview(machine, State, approveTarget ?? MessageState.Completed, MessageState.WaitingForSecondReview);
                break;
            case MessageState.WaitingForThirdReview:
                var third = machine.Configure(State).PermitReentry(MessageTrigger.Assign)
                    .PermitReentry(MessageTrigger.Reassign).Permit(MessageTrigger.StartReview, MessageState.ThirdReviewInProgress);
                if (approveTarget is not null) third.Permit(MessageTrigger.Undo, approveTarget.Value);
                break;
            case MessageState.ThirdReviewInProgress:
                ConfigureReview(machine, State, approveTarget ?? MessageState.Completed, MessageState.WaitingForThirdReview);
                break;
            case MessageState.Completed:
                if (approveTarget is not null) machine.Configure(State).Permit(MessageTrigger.Undo, approveTarget.Value);
                break;
            case MessageState.Rejected:
                machine.Configure(State).PermitReentry(MessageTrigger.Reassign);
                break;
        }
        return machine;
    }

    private static void ConfigureReview(StateMachine<MessageState, MessageTrigger> machine, MessageState state,
        MessageState approveTarget, MessageState cancelTarget) =>
        machine.Configure(state)
            .Permit(MessageTrigger.Approve, approveTarget)
            .Permit(MessageTrigger.Reject, MessageState.Rejected)
            .Permit(MessageTrigger.CancelReview, cancelTarget);

    private static void Fire(StateMachine<MessageState, MessageTrigger> machine, MessageTrigger trigger)
    {
        EnsureCanFire(machine, trigger);
        machine.Fire(trigger);
    }

    private static void EnsureCanFire(StateMachine<MessageState, MessageTrigger> machine, MessageTrigger trigger)
    {
        if (!machine.CanFire(trigger))
            throw new DomainRuleViolationException($"Trigger '{trigger}' is not allowed while message is in state '{machine.State}'.");
    }

    private static MessageState WaitingState(int level) => level switch
    {
        2 => MessageState.WaitingForSecondReview,
        3 => MessageState.WaitingForThirdReview,
        _ => throw new DomainRuleViolationException("Unsupported next review level.")
    };

    private static MessageState WaitingBeforeLevel(int level) => level switch
    {
        1 => MessageState.Assigned,
        2 => MessageState.WaitingForSecondReview,
        3 => MessageState.WaitingForThirdReview,
        _ => throw new DomainRuleViolationException("Unsupported review level.")
    };
}
