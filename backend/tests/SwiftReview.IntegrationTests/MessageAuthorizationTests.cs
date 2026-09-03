using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SwiftReview.Api.Authorization;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Messages;
using SwiftReview.Domain.Reviews;
using SwiftReview.Domain.Workflows;
using Xunit;

namespace SwiftReview.IntegrationTests;

public sealed class MessageAuthorizationTests
{
    [Fact]
    public async Task LevelTwo_RequiresAtomicPermissionAndFourEyes()
    {
        var (resource, branch, department) = WaitingForLevelTwo();
        Assert.False(await IsAuthorized(User(3, branch, department, Permissions.ReviewLevel1), resource, Permissions.ReviewLevel2, 2));
        Assert.False(await IsAuthorized(User(5, branch, department, Permissions.ReviewLevel2), resource, Permissions.ReviewLevel2, 2));
        Assert.True(await IsAuthorized(User(6, branch, department, Permissions.ReviewLevel2), resource, Permissions.ReviewLevel2, 2));
    }

    [Fact]
    public async Task CorrectPermission_DoesNotBypassBranchOrDepartmentScope()
    {
        var (resource, branch, department) = WaitingForLevelTwo();
        Assert.False(await IsAuthorized(User(6, branch + 1, department, Permissions.ReviewLevel2), resource, Permissions.ReviewLevel2, 2));
        Assert.False(await IsAuthorized(User(6, branch, department + 1, Permissions.ReviewLevel2), resource, Permissions.ReviewLevel2, 2));
    }

    private static async Task<bool> IsAuthorized(ClaimsPrincipal user, MessageAuthorizationResource resource, string permission, int level)
    {
        var requirement = new MessageActionRequirement(permission, level);
        var context = new AuthorizationHandlerContext([requirement], user, resource);
        await new MessageActionAuthorizationHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal User(int id, int branch, int department, params string[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()), new("branch", branch.ToString()), new("department", department.ToString()) };
        claims.AddRange(permissions.Select(x => new Claim("permission", x)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static (MessageAuthorizationResource Resource, int Branch, int Department) WaitingForLevelTwo()
    {
        var workflow = new WorkflowDefinition("Two", "MT199", 20).AddStep(1, 1).AddStep(2, 2);
        var message = new Message(1, workflow.Id);
        var reviews = new List<Review>(); message.Assign(2);
        var first = message.StartReview(1, 5, workflow, reviews, DateTimeOffset.UtcNow); reviews.Add(first);
        message.Approve(first, workflow, reviews, null, DateTimeOffset.UtcNow);
        return (new MessageAuthorizationResource(message, 10, 20, reviews), 10, 20);
    }
}
