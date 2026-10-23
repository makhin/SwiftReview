using Microsoft.EntityFrameworkCore;
using ORP.Domain.Identity;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class AuthorizationPermissionSetTests
{
    [Fact]
    public async Task PermissionSetMatchesIndividualChecksAndRequiresViewInTheSameDepartment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var user = new User("permissions", "Permissions");
        var admin = new User("permissions-admin", "Admin", true);
        var viewRole = new Role("PermissionSetView");
        var reviewRole = new Role("PermissionSetReview");
        var view = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageView, ct);
        var review = await db.Permissions.SingleAsync(p => p.Name == Permissions.ReviewLevel3, ct);
        var other = new Department("Other");
        var otherBranch = new Branch("Other");
        db.AddRange(user, admin, viewRole, reviewRole, other, otherBranch);
        await db.SaveChangesAsync(ct);
        var branch = await db.Branches.Where(b => b.Id != otherBranch.Id).Select(b => b.Id).FirstAsync(ct);
        var department = await db.Departments.Where(d => d.Id != other.Id).Select(d => d.Id).FirstAsync(ct);
        db.RolePermissions.AddRange(new RolePermission { RoleId = viewRole.Id, PermissionId = view.Id },
            new RolePermission { RoleId = reviewRole.Id, PermissionId = review.Id });
        db.UserRoles.AddRange(
            new UserRole { UserId = user.Id, BranchId = branch, DepartmentId = department, RoleId = viewRole.Id },
            new UserRole { UserId = user.Id, BranchId = branch, DepartmentId = other.Id, RoleId = reviewRole.Id });
        await db.SaveChangesAsync(ct);
        var queries = new UserAuthorizationQueries(db);
        var split = (await queries.GetPermissionsAsync(user.Id, branch, [department, other.Id], ct))!;
        Assert.True(split.Check(Permissions.ReviewLevel3).CanView);
        Assert.False(split.Check(Permissions.ReviewLevel3).HasPermission);
        foreach (var id in new[] { user.Id, admin.Id })
        foreach (var branchId in new[] { branch, otherBranch.Id })
        foreach (var scope in new int[][] { [], [department], [other.Id], [department, other.Id] })
        {
            var snapshot = (await queries.GetPermissionsAsync(id, branchId, scope, ct))!;
            foreach (var permission in Permissions.All)
                Assert.Equal(await queries.CheckAsync(id, branchId, scope, permission, ct), snapshot.Check(permission));
        }
        Assert.Null(await queries.GetPermissionsAsync(int.MaxValue, branch, [department], ct));
        db.UserRoles.Add(new UserRole { UserId = user.Id, BranchId = branch, DepartmentId = other.Id, RoleId = viewRole.Id });
        await db.SaveChangesAsync(ct);
        var joined = (await queries.GetPermissionsAsync(user.Id, branch, [department, other.Id], ct))!;
        Assert.True(joined.Check(Permissions.ReviewLevel3).HasPermission);
        Assert.False((await queries.GetPermissionsAsync(user.Id, branch, [department], ct))!.Check(Permissions.ReviewLevel3).HasPermission);
    }
}
