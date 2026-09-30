using ORP.Domain.Common;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Domain.Messages;

internal static class MessageReviewRules
{
    public static int ExpectedLevel(WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews)
    {
        var approved = reviews.Where(x => x.Status == ReviewStatus.Approved).Select(x => x.Level).ToHashSet();
        return workflow.RequiredLevels().FirstOrDefault(level => !approved.Contains(level));
    }

    public static int NextLevelAfterApproval(WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews, Review review)
    {
        var approved = reviews.Where(x => x.Status == ReviewStatus.Approved).Select(x => x.Level).Append(review.Level).ToHashSet();
        return workflow.RequiredLevels().FirstOrDefault(level => !approved.Contains(level));
    }

    public static UndoDecision CheckUndo(WorkflowDefinition workflow, IReadOnlyCollection<Review> reviews,
        Review review, int actorId, bool isGlobalAdministrator)
    {
        var levels = workflow.RequiredLevels();
        var latestApprovedLevel = levels.LastOrDefault(level =>
            reviews.Any(x => x.Level == level && x.Status == ReviewStatus.Approved));
        if (review.Status != ReviewStatus.Approved || latestApprovedLevel == 0 || review.Level != latestApprovedLevel)
            throw new DomainRuleViolationException("Only the latest approved workflow step can be undone.");

        var activeReview = reviews.SingleOrDefault(x => x.Status == ReviewStatus.InProgress);
        var administratorOverride = isGlobalAdministrator && activeReview is null;
        if (!administratorOverride)
        {
            if (workflow.UndoApprovalMode == UndoApprovalMode.Disabled ||
                (workflow.UndoApprovalMode == UndoApprovalMode.LatestNonFinal && review.Level == levels[^1]))
                throw new DomainRuleViolationException("The workflow policy does not allow undoing this confirmation.");
            if (!isGlobalAdministrator && workflow.UndoActorMode == UndoActorMode.OriginalReviewer && review.ReviewerId != actorId)
                throw new DomainRuleViolationException("Only the reviewer who approved can undo confirmation.");
            if (activeReview is not null && workflow.UndoActiveReviewMode != UndoActiveReviewMode.Cancel)
                throw new DomainRuleViolationException("The workflow policy blocks undo while a review is active.");
        }

        return new UndoDecision(activeReview, levels.FirstOrDefault(level => level > review.Level));
    }

    internal readonly record struct UndoDecision(Review? ActiveReview, int NextLevel);
}
