using Microsoft.EntityFrameworkCore;
using ORP.Domain.Identity;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class DepartmentAccessSqlTests
{
    [Fact]
    public async Task DepartmentReferenceRequiresViewPermissionAndDoesNotDuplicateScopes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var cs = await db.Departments.SingleAsync(ct);
        var paris = await db.Branches.SingleAsync(ct);
        var dc = new Department("DC");
        var tfo = new Department("TFO");
        var unassigned = new Department("Unassigned");
        var london = new Branch("London");
        var user = new User("scoped", "Scoped user");
        var outsider = new User("outsider", "Other user");
        var admin = new User("admin", "Administrator", isGlobalAdministrator: true);
        var viewRole = new Role("View only");
        var assignRole = new Role("Assign only");
        var emptyRole = new Role("No permissions");
        db.AddRange(dc, tfo, unassigned, london, user, outsider, admin, viewRole, assignRole, emptyRole);
        await db.SaveChangesAsync(ct);
        var view = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageView, ct);
        var assign = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageAssign, ct);
        db.RolePermissions.AddRange(
            new RolePermission { RoleId = viewRole.Id, PermissionId = view.Id },
            new RolePermission { RoleId = assignRole.Id, PermissionId = assign.Id });
        db.UserRoles.AddRange(
            new UserRole { UserId = user.Id, BranchId = paris.Id, DepartmentId = cs.Id, RoleId = viewRole.Id },
            new UserRole { UserId = user.Id, BranchId = london.Id, DepartmentId = cs.Id, RoleId = viewRole.Id },
            new UserRole { UserId = user.Id, BranchId = paris.Id, DepartmentId = dc.Id, RoleId = assignRole.Id },
            new UserRole { UserId = user.Id, BranchId = paris.Id, DepartmentId = tfo.Id, RoleId = emptyRole.Id },
            new UserRole { UserId = outsider.Id, BranchId = paris.Id, DepartmentId = unassigned.Id, RoleId = viewRole.Id });
        await db.SaveChangesAsync(ct);

        var accessService = new UserAccessService(db);
        var access = (await accessService.GetByIdAsync(user.Id, ct))!;
        var queries = new ReferenceDataQueries(db);
        var departments = await queries.GetDepartmentsAsync(access, ct);
        Assert.Equal(cs.Id, Assert.Single(departments).Id);
        Assert.False(access.CanAccess(paris.Id, dc.Id));
        Assert.False(access.CanAccess(paris.Id, tfo.Id));
        Assert.DoesNotContain(unassigned.Id, access.DepartmentIds);

        var adminAccess = (await accessService.GetByIdAsync(admin.Id, ct))!;
        Assert.Equal(await db.Departments.CountAsync(ct),
            (await queries.GetDepartmentsAsync(adminAccess, ct)).Count);

        db.UserRoles.RemoveRange(await db.UserRoles.Where(r => r.UserId == user.Id && r.RoleId == viewRole.Id).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        access = (await accessService.GetByIdAsync(user.Id, ct))!;
        Assert.Empty(await queries.GetDepartmentsAsync(access, ct));
    }
}
