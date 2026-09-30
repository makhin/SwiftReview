using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence;

internal sealed class UndoReviewCandidate
{
    public long MessageId { get; init; }
    public long ReviewId { get; init; }
}

internal static class UndoReviewQueries
{
    // SQL counterpart of Message.UndoLastApproval. Keep policy parity covered by SQL tests.
    public static IQueryable<UndoReviewCandidate> ReadUndoCandidates(this ORPDbContext db, UserAccess access) =>
        from message in db.ReadAccessibleMessages(access.UserId, Permissions.ReviewUndo)
        join workflow in db.WorkflowDefinitions on message.WorkflowDefinitionId equals workflow.Id
        join review in db.Reviews on message.Id equals review.MessageId
        where workflow.IsActive && review.Status == ReviewStatus.Approved &&
            (message.State == MessageState.WaitingForSecondReview || message.State == MessageState.WaitingForThirdReview ||
             message.State == MessageState.Completed || message.State == MessageState.SecondReviewInProgress ||
             message.State == MessageState.ThirdReviewInProgress) &&
            db.WorkflowSteps.Any(step => step.WorkflowDefinitionId == workflow.Id && step.Required && step.ReviewLevel == review.Level) &&
            !db.Reviews.Any(later => later.MessageId == message.Id && later.Status == ReviewStatus.Approved && later.Level > review.Level &&
                db.WorkflowSteps.Any(step => step.WorkflowDefinitionId == workflow.Id && step.Required && step.ReviewLevel == later.Level)) &&
            (access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                role.DepartmentId == workflow.DepartmentId && (workflow.BranchId == null || role.BranchId == workflow.BranchId) &&
                role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.MessageView))) &&
            ((access.IsGlobalAdministrator && message.ActiveReviewId == null) ||
                ((workflow.UndoApprovalMode == UndoApprovalMode.Latest ||
                    (workflow.UndoApprovalMode == UndoApprovalMode.LatestNonFinal &&
                        db.WorkflowSteps.Any(step => step.WorkflowDefinitionId == workflow.Id && step.Required && step.ReviewLevel > review.Level))) &&
                 (access.IsGlobalAdministrator || workflow.UndoActorMode == UndoActorMode.AnyAuthorizedUser || review.ReviewerId == access.UserId) &&
                 (message.ActiveReviewId == null || workflow.UndoActiveReviewMode == UndoActiveReviewMode.Cancel))) &&
            ((message.ActiveReviewId == null &&
                (message.State == MessageState.WaitingForSecondReview || message.State == MessageState.WaitingForThirdReview || message.State == MessageState.Completed)) ||
             (message.ActiveReviewId != null &&
                ((message.ActiveReviewLevel == 2 && message.State == MessageState.SecondReviewInProgress) ||
                 (message.ActiveReviewLevel == 3 && message.State == MessageState.ThirdReviewInProgress)) &&
                message.ActiveReviewLevel == db.WorkflowSteps.Where(step => step.WorkflowDefinitionId == workflow.Id && step.Required && step.ReviewLevel > review.Level)
                    .OrderBy(step => step.Order).Select(step => (int?)step.ReviewLevel).FirstOrDefault()))
        select new UndoReviewCandidate { MessageId = message.Id, ReviewId = review.Id };
}
