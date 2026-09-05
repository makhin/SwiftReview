using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Application.Assignments;

public static class ReviewAssignmentRules
{
    public static int? ReviewLevelForState(MessageState state) => state switch
    {
        MessageState.New or MessageState.Assigned or MessageState.FirstReviewInProgress => 1,
        MessageState.WaitingForSecondReview or MessageState.SecondReviewInProgress => 2,
        MessageState.WaitingForThirdReview or MessageState.ThirdReviewInProgress => 3,
        _ => null
    };

    public static string PermissionForLevel(int level) => level switch
    {
        1 => Permissions.ReviewLevel1,
        2 => Permissions.ReviewLevel2,
        3 => Permissions.ReviewLevel3,
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public static int[] ApprovedReviewerIds(IReadOnlyCollection<Review> reviews) =>
        reviews.Where(review => review.Status == ReviewStatus.Approved)
            .Select(review => review.ReviewerId)
            .Distinct()
            .ToArray();
}
