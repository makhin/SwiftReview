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
    Rejected
}
