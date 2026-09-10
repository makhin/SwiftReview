using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Application.Assignments;

public static class ReviewAssignmentRules
{
    public static int? AssignmentLevelForState(MessageState state) => state switch
    {
        MessageState.New or MessageState.Assigned => 1,
        MessageState.WaitingForSecondReview => 2,
        MessageState.WaitingForThirdReview => 3,
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

    public static bool IsEligible(UserAccess target, MessageSourceDto source, int reviewLevel,
        IReadOnlyCollection<int> approvedReviewerIds, int actorId, int? currentAssigneeId) =>
        target.UserId != actorId && target.UserId != currentAssigneeId &&
        target.HasPermission(PermissionForLevel(reviewLevel), source.BranchId, source.DepartmentId) &&
        target.CanAccess(source.BranchId, source.DepartmentId) &&
        !approvedReviewerIds.Contains(target.UserId);
}
