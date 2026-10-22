using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Domain.Assignments;
using ORP.Domain.Common;
using ORP.Domain.Workflows;
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

    public static int? CurrentLevel(MessageState state) => state switch
    {
        MessageState.FirstReviewInProgress => 1,
        MessageState.SecondReviewInProgress => 2,
        MessageState.ThirdReviewInProgress => 3,
        _ => AssignmentLevelForState(state)
    };

    public static int RequireAssignableLevel(Message message, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, int? requestedLevel)
    {
        var current = CurrentLevel(message.State)
            ?? throw new DomainRuleViolationException($"Assignment is not allowed while message is in state '{message.State}'.");
        var level = requestedLevel ?? current;
        if (level is < 1 or > 3) throw new ValidationException("Level must be between 1 and 3.");
        if (!workflow.IsActive || !workflow.RequiredLevels().Contains(level) || level < current ||
            reviews.Any(r => r.Level == level && r.Status is ReviewStatus.Approved or ReviewStatus.InProgress or ReviewStatus.Rejected))
            throw new DomainRuleViolationException("This workflow has no assignable confirmation at the requested level.");
        return level;
    }

    public static int[] ExcludedReviewerIds(WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, IEnumerable<Assignment> assignments, int level) =>
        workflow.AllowReviewerReuse ? [] : ApprovedReviewerIds(reviews)
            .Concat(assignments.Where(a => a.EndedAt is null && a.Level != level).Select(a => a.AssignedTo))
            .Distinct().ToArray();

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

    public static bool IsEligible(int targetId, UserPermissionCheck target,
        IReadOnlyCollection<int> approvedReviewerIds, int? currentAssigneeId) =>
        targetId != currentAssigneeId && (target.IsGlobalAdministrator || (
        target.HasPermission &&
        target.CanView &&
        !approvedReviewerIds.Contains(targetId)));
}
