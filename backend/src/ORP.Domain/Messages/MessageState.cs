namespace ORP.Domain.Messages;

public enum MessageState
{
    New,
    Assigned,
    FirstReviewInProgress,
    WaitingForSecondReview,
    SecondReviewInProgress,
    WaitingForThirdReview,
    ThirdReviewInProgress,
    Completed,
    Rejected,
    ViewOnly
}

public static class MessageStateExtensions
{
    public static int? ReviewLevel(this MessageState state) => state switch
    {
        MessageState.New or MessageState.Assigned or MessageState.FirstReviewInProgress => 1,
        MessageState.WaitingForSecondReview or MessageState.SecondReviewInProgress => 2,
        MessageState.WaitingForThirdReview or MessageState.ThirdReviewInProgress => 3,
        _ => null
    };

    public static bool IsReviewInProgress(this MessageState state) => state is
        MessageState.FirstReviewInProgress or MessageState.SecondReviewInProgress or MessageState.ThirdReviewInProgress;

    public static int? AssignableReviewLevel(this MessageState state) => state.IsReviewInProgress() ? null : state.ReviewLevel();
}
