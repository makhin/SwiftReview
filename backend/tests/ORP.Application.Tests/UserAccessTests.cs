using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using Xunit;

namespace ORP.Application.Tests;

public sealed class UserAccessTests
{
    [Fact]
    public void Permissions_StayWithinExactScope()
    {
        var access = new UserAccess(1, "reviewer", "Reviewer", false,
            [new UserScopeAccess(10, 20, [1], [Permissions.MessageView, Permissions.ReviewLevel1]),
             new UserScopeAccess(11, 30, [2], [Permissions.MessageView, Permissions.ReviewLevel2])]);

        Assert.True(access.CanAccess(10, 20));
        Assert.True(access.CanAccess(11, 30));
        Assert.False(access.CanAccess(10, 30));
        Assert.False(access.CanAccess(11, 20));
        Assert.False(access.HasPermission(Permissions.ReviewLevel2, 10, 20));
    }

    [Fact]
    public void GlobalAdministrator_BypassesBusinessAccessWithoutRoles()
    {
        var access = new UserAccess(1, "admin", "Administrator", true, []);
        Assert.True(access.CanAccess(1, 1));
        Assert.True(access.CanAccess(99, 99));
        Assert.All(Permissions.All, permission => Assert.True(access.HasPermission(permission, 99, 99)));
        Assert.True(access.HasPermission("future.permission", 99, 99));
        Assert.Equal(Permissions.All.Order(), access.Permissions.Order());
    }
}
