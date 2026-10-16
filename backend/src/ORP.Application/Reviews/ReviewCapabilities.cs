using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Domain.Common;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;

namespace ORP.Application.Reviews;

public sealed record ReviewCapabilities(ConfirmationMode ConfirmationMode, bool CanConfirm, int? ConfirmationLevel, bool CanReview)
{
    public static ReviewCapabilities For(Message message, WorkflowDefinition workflow,
        IReadOnlyCollection<Review> reviews, int branchId, IReadOnlyList<int> departmentIds, UserAccess access)
    {
        var unavailable = new ReviewCapabilities(workflow.ConfirmationMode, false, null, false);
        if (!workflow.IsActive || workflow.ConfirmationMode == ConfirmationMode.None) return unavailable;
        var level = message.State switch
        {
            MessageState.Assigned or MessageState.FirstReviewInProgress => 1,
            MessageState.WaitingForSecondReview or MessageState.SecondReviewInProgress => 2,
            MessageState.WaitingForThirdReview or MessageState.ThirdReviewInProgress => 3,
            _ => 0
        };
        if (level == 0) return unavailable;
        var permitted = access.IsGlobalAdministrator || departmentIds.Any(department =>
            access.CanAccess(branchId, department) &&
            access.HasPermission(MessageAuthorizationService.ReviewPermission(level), branchId, department));
        if (!permitted) return unavailable;
        var active = reviews.SingleOrDefault(x => x.Status == ReviewStatus.InProgress);
        if (active is not null)
        {
            var canResume = workflow.ConfirmationMode == ConfirmationMode.ReviewSession && active.Level == level &&
                (access.IsGlobalAdministrator || (active.ReviewerId == access.UserId &&
                    (workflow.ConfirmationActorMode == ConfirmationActorMode.AnyAuthorizedUser || message.CurrentAssigneeId == access.UserId) &&
                    (workflow.AllowReviewerReuse || !reviews.Any(x => x.Status == ReviewStatus.Approved && x.ReviewerId == access.UserId))));
            return unavailable with { CanReview = canResume };
        }
        try
        {
            message.ValidateReviewStart(level, access.UserId, workflow, reviews, isGlobalAdministrator: access.IsGlobalAdministrator);
            return workflow.ConfirmationMode == ConfirmationMode.Immediate
                ? unavailable with { CanConfirm = true, ConfirmationLevel = level }
                : unavailable with { CanReview = true };
        }
        catch (DomainRuleViolationException) { return unavailable; }
    }
}
