using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Identity;
using ORP.Domain.Assignments;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Application.Reviews;

public sealed record ReviewCapabilities(ConfirmationMode ConfirmationMode, bool CanConfirm, int? ConfirmationLevel, bool CanReview)
{
    public long? UndoReviewId { get; init; }

    public static ReviewCapabilities For(Message message, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, int branchId, UserAccess access,
        IReadOnlyCollection<Assignment>? assignments = null)
    {
        bool Permitted(string permission) => access.IsGlobalAdministrator || message.ProcessingDepartmentIds.Any(department =>
            access.CanAccess(branchId, department) && access.HasPermission(permission, branchId, department));

        var unavailable = new ReviewCapabilities(workflow.ConfirmationMode, false, null, false);
        if (!workflow.IsActive || workflow.ConfirmationMode == ConfirmationMode.None) return unavailable;
        if (Permitted(Permissions.ReviewUndo))
            unavailable = unavailable with { UndoReviewId = reviews
                .Where(r => r.Status == ReviewStatus.Approved)
                .OrderByDescending(r => r.Level)
                .FirstOrDefault(r => message.GetUndoError(r, workflow, reviews, access.UserId, access.IsGlobalAdministrator) is null)?.Id };
        if (message.State.ReviewLevel() is not { } level ||
            !Permitted(ReviewAssignmentRules.PermissionForLevel(level))) return unavailable;
        if (reviews.Any(x => x.Status == ReviewStatus.InProgress))
            return unavailable with { CanReview = message.CanResumeReview(level, access.UserId, workflow, reviews, access.IsGlobalAdministrator) };
        if (message.CurrentAssigneeId is null && !access.IsGlobalAdministrator &&
            ReviewAssignmentRules.ExcludedReviewerIds(workflow, reviews, assignments ?? [], level).Contains(access.UserId))
            return unavailable;
        if (message.GetReviewStartError(level, access.UserId, workflow, reviews, access.IsGlobalAdministrator,
                allowSelfAssignment: Permitted(Permissions.MessageAssign)) is not null)
            return unavailable;
        return workflow.ConfirmationMode == ConfirmationMode.Immediate
            ? unavailable with { CanConfirm = true, ConfirmationLevel = level }
            : unavailable with { CanReview = true };
    }
}
