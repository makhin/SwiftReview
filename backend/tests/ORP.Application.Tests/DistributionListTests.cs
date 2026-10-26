using ORP.Application.Abstractions;
using ORP.Application.DistributionLists;
using ORP.Domain.Identity;
using Xunit;

namespace ORP.Application.Tests;

public sealed class DistributionListTests
{
    [Theory]
    [InlineData(Permissions.MessageForward, true, false)]
    [InlineData(Permissions.DistributionListManage, true, true)]
    [InlineData(Permissions.MessageView, false, false)]
    [InlineData("", false, false)]
    public void AccessRequiresPermissionInTheTargetDepartment(string permission, bool read, bool manage)
    {
        var access = new UserAccess(1, "user", "User", false,
            [new UserScopeAccess(1, 10, [], [permission]),
                new UserScopeAccess(2, 20, [], [Permissions.MessageForward, Permissions.DistributionListManage])]);
        Assert.Equal(read, DistributionListAccess.CanRead(access, 10));
        Assert.Equal(manage, DistributionListAccess.CanManage(access, 10));
        Assert.False(DistributionListAccess.CanRead(access, 30));
        Assert.False(DistributionListAccess.CanManage(access, 30));
    }

    [Fact]
    public void AccessCanComeFromAnyBranchAndGlobalAdminNeedsNoScope()
    {
        var access = new UserAccess(1, "user", "User", false,
            [new UserScopeAccess(1, 10, [], [Permissions.MessageView]),
                new UserScopeAccess(2, 10, [], [Permissions.DistributionListManage])]);
        Assert.True(DistributionListAccess.CanRead(access, 10));
        Assert.True(DistributionListAccess.CanManage(access, 10));
        var admin = access with { IsGlobalAdministrator = true, Scopes = [] };
        Assert.True(DistributionListAccess.CanRead(admin, 30));
        Assert.True(DistributionListAccess.CanManage(admin, 30));
        Assert.Contains(Permissions.MessageForward, admin.Permissions);
        Assert.Contains(Permissions.DistributionListManage, admin.Permissions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NameIsRequired(string? name) => AssertInvalid(new(name!, []), "Name");

    [Fact]
    public void NameAndEmailLimitsApplyAfterTrimming()
    {
        AssertValid(new(" " + new string('a', 200) + " ", [" user@example.com "]));
        AssertInvalid(new(new string('a', 201), []), "Name");
        AssertValid(new("Empty", []));
        AssertInvalid(new("Missing", null!), "Emails");
        AssertInvalid(new("Too many", Enumerable.Range(0, 1001).Select(i => $"user{i}@example.com").ToArray()), "Emails");
        AssertValid(new("Maximum", Enumerable.Range(0, 1000).Select(i => $"user{i}@example.com").ToArray()));
        var maximumEmail = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 61);
        Assert.Equal(254, maximumEmail.Length);
        AssertValid(new("Boundary", [maximumEmail]));
        AssertInvalid(new("Over boundary", [maximumEmail + "d"]), "Emails[0]");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("Name <user@example.com>")]
    [InlineData("<user@example.com>")]
    [InlineData("user@example.com,other@example.com")]
    [InlineData("user@example.com;other@example.com")]
    [InlineData("user@example.com\r\nBcc:other@example.com")]
    public void ItemsMustBeSingleEmailAddresses(string? email) => AssertInvalid(new("List", [email!]), "Emails[0]");

    [Fact]
    public void DuplicateEmailComparisonIgnoresCaseAndSurroundingSpaces() =>
        AssertInvalid(new("List", ["User@example.com", " user@EXAMPLE.com "]), "Emails");

    private static void AssertValid(SaveDistributionListRequest request) =>
        Assert.True(new SaveDistributionListValidator().Validate(request).IsValid);

    private static void AssertInvalid(SaveDistributionListRequest request, string property)
    {
        var result = new SaveDistributionListValidator().Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }
}
