using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using Xunit;

namespace ORP.Application.Tests;

public sealed class UserAccessTests
{
    [Fact]
    public void AllDepartmentPermission_BypassesDepartmentsButNotBranches()
    {
        var access = new UserAccess(1, "admin",
            new HashSet<string> { Permissions.MessageAccessAllDepartments },
            new HashSet<int> { 10 }, new HashSet<int> { 20 });

        Assert.True(access.CanAccess(10, 99));
        Assert.False(access.CanAccess(11, 20));
    }

    [Fact]
    public void RegularUser_CanAccessEveryAssignedDepartmentOnly()
    {
        var access = new UserAccess(1, "reviewer", new HashSet<string>(),
            new HashSet<int> { 10 }, new HashSet<int> { 20, 30 });

        Assert.True(access.CanAccess(10, 20));
        Assert.True(access.CanAccess(10, 30));
        Assert.False(access.CanAccess(10, 40));
    }
}
