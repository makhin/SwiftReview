using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Infrastructure.Persistence;

namespace ORP.Infrastructure.Identity;

public sealed class UserAccessService(ORPDbContext db) : IUserAccessService
{
    public async Task<UserAccess?> GetByIdAsync(int id, CancellationToken ct) => Map(await BaseQuery().SingleOrDefaultAsync(x => x.Id == id, ct));

    public async Task<string> GetRolesAndDepartmentsAsync(UserAccess access, CancellationToken ct)
    {
        if (access.IsGlobalAdministrator) return FormatRolesAndDepartments(true, []);

        var labels = await (from assignment in db.UserRoles.AsNoTracking()
            join department in db.Departments on assignment.DepartmentId equals department.Id
            where assignment.UserId == access.UserId
            select department.Name + " " + assignment.Role.Name).ToArrayAsync(ct);
        return FormatRolesAndDepartments(false, labels);
    }

    internal static string FormatRolesAndDepartments(bool isGlobalAdministrator, IEnumerable<string> labels) =>
        isGlobalAdministrator ? "Global Admin" : string.Join(", ", labels.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private IQueryable<Domain.Identity.User> BaseQuery() => db.Users.AsNoTracking()
        .Include(x => x.Roles).ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
        .AsSplitQuery();
    private static UserAccess? Map(Domain.Identity.User? x) => x is null ? null : new UserAccess(x.Id, x.UserName,
        x.DisplayName, x.IsGlobalAdministrator,
        x.Roles.GroupBy(r => new { r.BranchId, r.DepartmentId })
            .Select(group => new UserScopeAccess(group.Key.BranchId, group.Key.DepartmentId,
                group.Select(r => r.RoleId).Order().ToArray(),
                group.SelectMany(r => r.Role.Permissions.Select(p => p.Permission.Name))
                    .Distinct().Order().ToArray())).OrderBy(s => s.BranchId).ThenBy(s => s.DepartmentId).ToArray());
}
