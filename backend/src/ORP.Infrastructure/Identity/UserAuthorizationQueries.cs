using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Infrastructure.Persistence;

namespace ORP.Infrastructure.Identity;

public sealed class UserAuthorizationQueries(ORPDbContext db) : IUserAuthorizationQueries
{
    private static IQueryable<UserIdentity> Identities(IQueryable<User> users) => users.AsNoTracking()
        .Select(u => new UserIdentity(u.Id, u.UserName, u.DisplayName, u.IsGlobalAdministrator));

    public Task<UserIdentity?> GetIdentityAsync(int userId, CancellationToken ct) =>
        Identities(db.Users.Where(u => u.Id == userId)).SingleOrDefaultAsync(ct);

    public Task<UserIdentity?> GetIdentityAsync(string userName, CancellationToken ct) =>
        Identities(db.Users.Where(u => u.UserName == userName)).SingleOrDefaultAsync(ct);

    public Task<UserPermissionCheck?> CheckAsync(int userId, int branchId, int departmentId, string permission, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new UserPermissionCheck(u.IsGlobalAdministrator,
                db.UserRoles.Any(r => r.UserId == u.Id && r.BranchId == branchId && r.DepartmentId == departmentId &&
                    r.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)),
                db.UserRoles.Any(r => r.UserId == u.Id && r.BranchId == branchId && r.DepartmentId == departmentId &&
                    r.Role.Permissions.Any(p => p.Permission.Name == permission))))
            .SingleOrDefaultAsync(ct);

    public Task<bool> CanAccessWorkflowAsync(int userId, int workflowId, CancellationToken ct) =>
        db.WorkflowDefinitions.AnyAsync(w => w.Id == workflowId && db.UserRoles.Any(r => r.UserId == userId &&
            r.DepartmentId == w.DepartmentId && (w.BranchId == null || r.BranchId == w.BranchId) &&
            r.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)), ct);
}
