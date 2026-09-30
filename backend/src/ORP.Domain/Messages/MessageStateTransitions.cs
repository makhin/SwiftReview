using ORP.Domain.Common;

namespace ORP.Domain.Messages;

internal static class MessageStateTransitions
{
    public static MessageState Assign(MessageState state) => state switch
    {
        MessageState.New => MessageState.Assigned,
        MessageState.Assigned or MessageState.WaitingForSecondReview or MessageState.WaitingForThirdReview => state,
        _ => throw Invalid("Assign", state)
    };

    public static MessageState StartReview(MessageState state, int level)
    {
        if (state != WaitingBeforeLevel(level)) throw Invalid("StartReview", state);
        return ReviewState(level);
    }

    public static MessageState Approve(MessageState state, int level, int nextLevel)
    {
        EnsureActiveLevel(state, level);
        return nextLevel == 0 ? MessageState.Completed : WaitingAfterApproval(nextLevel);
    }

    public static MessageState Reject(MessageState state, int level)
    {
        EnsureActiveLevel(state, level);
        return MessageState.Rejected;
    }

    public static MessageState CancelReview(MessageState state, int level)
    {
        EnsureActiveLevel(state, level);
        return WaitingBeforeLevel(level);
    }

    public static MessageState Undo(MessageState state, int approvedLevel, int nextLevel, int? activeLevel)
    {
        if (activeLevel is not null)
        {
            if (activeLevel != nextLevel || state != ReviewState(activeLevel.Value))
                throw new DomainRuleViolationException("Only the active next workflow step can be cancelled by undo.");
        }
        else
        {
            if (state is MessageState.FirstReviewInProgress or MessageState.SecondReviewInProgress or MessageState.ThirdReviewInProgress)
                throw new DomainRuleViolationException("The active review was not found.");
            var expected = nextLevel == 0 ? MessageState.Completed : WaitingAfterApproval(nextLevel);
            if (state != expected) throw Invalid("Undo", state);
        }
        return WaitingBeforeLevel(approvedLevel);
    }

    private static void EnsureActiveLevel(MessageState state, int level)
    {
        if (state != ReviewState(level))
            throw new DomainRuleViolationException("This review level is not currently active.");
    }

    private static MessageState ReviewState(int level) => level switch
    {
        1 => MessageState.FirstReviewInProgress,
        2 => MessageState.SecondReviewInProgress,
        3 => MessageState.ThirdReviewInProgress,
        _ => throw new DomainRuleViolationException("Unsupported review level.")
    };

    private static MessageState WaitingBeforeLevel(int level) => level switch
    {
        1 => MessageState.Assigned,
        2 => MessageState.WaitingForSecondReview,
        3 => MessageState.WaitingForThirdReview,
        _ => throw new DomainRuleViolationException("Unsupported review level.")
    };

    private static MessageState WaitingAfterApproval(int level) => level switch
    {
        2 => MessageState.WaitingForSecondReview,
        3 => MessageState.WaitingForThirdReview,
        _ => throw new DomainRuleViolationException("Unsupported next review level.")
    };

    private static DomainRuleViolationException Invalid(string action, MessageState state) =>
        new($"Action '{action}' is not allowed while message is in state '{state}'.");
}
