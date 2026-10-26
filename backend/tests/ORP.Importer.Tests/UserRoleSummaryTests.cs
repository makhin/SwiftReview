using ORP.Infrastructure.Identity;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class UserRoleSummaryTests
{
    [Fact]
    public void SummaryIncludesDistinctDepartmentRolePairsInStableOrder()
    {
        var result = UserAccessService.FormatRolesAndDepartments(false,
            ["TFO Reviewer", "CS Reviewer", "TFO Reviewer", "CS Approver"]);

        Assert.Equal("CS Approver, CS Reviewer, TFO Reviewer", result);
    }

    [Fact]
    public void GlobalAdministratorLabelReplacesAssignedRoles()
    {
        Assert.Equal("Global Admin", UserAccessService.FormatRolesAndDepartments(true, ["TFO Reviewer"]));
        Assert.Equal("Global Admin", UserAccessService.FormatRolesAndDepartments(true, []));
    }

    [Fact]
    public void UserWithoutRolesHasEmptySummary()
    {
        Assert.Equal(string.Empty, UserAccessService.FormatRolesAndDepartments(false, []));
    }
}
