using Microsoft.AspNetCore.Authorization;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Api.Authorization;

public sealed record MessageActionRequirement(string Permission, int? ReviewLevel = null) : IAuthorizationRequirement;
public sealed record MessageAuthorizationResource(Message Message, int BranchId, int DepartmentId,
    IReadOnlyCollection<Review> Reviews);

public sealed class MessageActionAuthorizationHandler : AuthorizationHandler<MessageActionRequirement, MessageAuthorizationResource>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MessageActionRequirement requirement, MessageAuthorizationResource resource)
    {
        var permission = context.User.HasClaim("permission", requirement.Permission);
        var branch = context.User.HasClaim("branch", resource.BranchId.ToString());
        var department = context.User.HasClaim("department", resource.DepartmentId.ToString()) ||
            context.User.HasClaim("permission", Permissions.MessageAccessAllDepartments);
        var stateOk = requirement.ReviewLevel switch
        {
            1 => resource.Message.State is MessageState.Assigned or MessageState.FirstReviewInProgress,
            2 => resource.Message.State is MessageState.WaitingForSecondReview or MessageState.SecondReviewInProgress,
            3 => resource.Message.State is MessageState.WaitingForThirdReview or MessageState.ThirdReviewInProgress,
            _ => true
        };
        var currentId = int.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        var fourEyes = requirement.ReviewLevel is null || resource.Reviews.All(x => x.Status != ReviewStatus.Approved || x.ReviewerId != currentId);
        if (permission && branch && department && stateOk && fourEyes) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class ReviewPermissions
{
    public static string ForLevel(int level) => level switch { 1 => Permissions.ReviewLevel1, 2 => Permissions.ReviewLevel2, 3 => Permissions.ReviewLevel3, _ => "invalid" };
}
