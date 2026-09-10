using System.Data;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Administration;
using ORP.Application.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Identity;
using ORP.Domain.Reviews;
using ORP.Infrastructure.Persistence;

namespace ORP.Infrastructure.Identity;

public sealed class UserAdministrationService(ORPDbContext db, ICurrentUser current,
    IUserAccessService accessService, IClock clock, ICorrelationContext correlation) : IUserAdministrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task RequireAdminAsync(CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == current.UserId && u.IsGlobalAdministrator, ct))
            throw new UnauthorizedAccessException("Global administrator access is required.");
    }

    public async Task<AccessCatalogDto> GetCatalogAsync(CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        var roles = await db.Roles.AsNoTracking().Include(r => r.Permissions).ThenInclude(p => p.Permission)
            .OrderBy(r => r.Name).ToListAsync(ct);
        return new AccessCatalogDto(roles.Select(r => new RoleDetailsDto(r.Id, r.Name,
                r.Permissions.Select(p => p.Permission.Name).Order().ToArray())).ToArray(), Permissions.All,
            await db.Branches.OrderBy(b => b.Name).Select(b => new ReferenceItemDto(b.Id, b.Name)).ToListAsync(ct),
            await db.Departments.OrderBy(d => d.Name).Select(d => new ReferenceItemDto(d.Id, d.Name)).ToListAsync(ct));
    }

    public async Task<UserAccessDetailsDto> GetUserAsync(int userId, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        var access = await accessService.GetByIdAsync(userId, ct)
            ?? throw new ResourceNotFoundException("User was not found.");
        return new UserAccessDetailsDto(userId, access.UserName, access.DisplayName,
            access.Scopes.Select(s => new ScopedRoleAssignmentDto(s.BranchId, s.DepartmentId, s.RoleIds)).ToArray(),
            access.Scopes);
    }

    public Task UpdateUserAsync(int userId, UpdateUserAccessRequest request, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            await RequireAdminAsync(ct);
            if (!await db.Users.AnyAsync(u => u.Id == userId, ct)) throw new ResourceNotFoundException("User was not found.");
            var assignments = request.Assignments;
            if (assignments is null || assignments.Count > 200 || assignments.Any(a => a is null ||
                    a.BranchId <= 0 || a.DepartmentId <= 0 || a.RoleIds is null || a.RoleIds.Count is < 1 or > 50 ||
                    a.RoleIds.Any(id => id <= 0) || a.RoleIds.Distinct().Count() != a.RoleIds.Count) ||
                assignments.Select(a => (a.BranchId, a.DepartmentId)).Distinct().Count() != assignments.Count)
                throw new ValidationException("Provide unique branch/department pairs with unique, valid role IDs.");
            var branchIds = assignments.Select(a => a.BranchId).Distinct().ToArray();
            var departmentIds = assignments.Select(a => a.DepartmentId).Distinct().ToArray();
            var roleIds = assignments.SelectMany(a => a.RoleIds).Distinct().ToArray();
            if (await db.Branches.CountAsync(b => branchIds.Contains(b.Id), ct) != branchIds.Length ||
                await db.Departments.CountAsync(d => departmentIds.Contains(d.Id), ct) != departmentIds.Length ||
                await db.Roles.CountAsync(r => roleIds.Contains(r.Id), ct) != roleIds.Length)
                throw new ValidationException("Unknown branch, department or role.");

            var roles = await db.Roles.Include(r => r.Permissions).ThenInclude(p => p.Permission)
                .Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
            var proposed = assignments.SelectMany(a => a.RoleIds.Select(roleId => new UserRole
                { UserId = userId, BranchId = a.BranchId, DepartmentId = a.DepartmentId, RoleId = roleId, Role = roles[roleId] }))
                .ToArray();
            await EnsureActiveReviewsAsync([userId], proposed, ct);
            var old = await db.UserRoles.Where(r => r.UserId == userId).ToListAsync(ct);
            var before = AssignmentSnapshot(old);
            var desired = proposed.Select(Key).ToHashSet();
            var existing = old.Select(Key).ToHashSet();
            db.UserRoles.RemoveRange(old.Where(r => !desired.Contains(Key(r))));
            db.UserRoles.AddRange(proposed.Where(r => !existing.Contains(Key(r))));
            AddAudit(userId, null, before, AssignmentSnapshot(proposed));
            await db.SaveChangesAsync(ct);
        }, ct);

    public Task UpdateRoleAsync(int roleId, UpdateRolePermissionsRequest request, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            await RequireAdminAsync(ct);
            if (request.Permissions is null || request.Permissions.Count > Permissions.All.Length ||
                request.Permissions.Distinct().Count() != request.Permissions.Count ||
                request.Permissions.Any(p => !Permissions.All.Contains(p)))
                throw new ValidationException("Select unique permissions from the fixed permission catalog.");
            var role = await db.Roles.Include(r => r.Permissions).ThenInclude(p => p.Permission)
                .SingleOrDefaultAsync(r => r.Id == roleId, ct) ?? throw new ResourceNotFoundException("Role was not found.");
            var before = role.Permissions.Select(p => p.Permission.Name).Order().ToArray();
            var selected = await db.Permissions.Where(p => request.Permissions.Contains(p.Name)).ToListAsync(ct);
            if (selected.Count != request.Permissions.Count) throw new ValidationException("Permission catalog is incomplete.");

            var affectedIds = await db.UserRoles.Where(r => r.RoleId == roleId).Select(r => r.UserId).Distinct().ToArrayAsync(ct);
            var assignments = await db.UserRoles.Include(r => r.Role).ThenInclude(r => r.Permissions).ThenInclude(p => p.Permission)
                .Where(r => affectedIds.Contains(r.UserId)).ToListAsync(ct);
            await EnsureActiveReviewsAsync(affectedIds, assignments, ct, roleId, request.Permissions);

            var keep = selected.Select(p => p.Id).ToHashSet();
            var oldIds = role.Permissions.Select(p => p.PermissionId).ToHashSet();
            db.RolePermissions.RemoveRange(role.Permissions.Where(p => !keep.Contains(p.PermissionId)).ToArray());
            db.RolePermissions.AddRange(selected.Where(p => !oldIds.Contains(p.Id)).Select(p =>
                new RolePermission { RoleId = roleId, PermissionId = p.Id }));
            AddAudit(null, roleId, before, request.Permissions.Order().ToArray());
            await db.SaveChangesAsync(ct);
        }, ct);

    private async Task EnsureActiveReviewsAsync(int[] userIds, IReadOnlyCollection<UserRole> assignments,
        CancellationToken ct, int? changedRoleId = null, IReadOnlyList<string>? changedPermissions = null)
    {
        var active = await (from review in db.Reviews
            join source in db.SwiftMessages on review.MessageId equals source.MessageId
            where review.Status == ReviewStatus.InProgress && userIds.Contains(review.ReviewerId) &&
                !db.Users.Any(u => u.Id == review.ReviewerId && u.IsGlobalAdministrator)
            select new { review.ReviewerId, review.Level, source.BranchId, source.DepartmentId }).ToListAsync(ct);
        foreach (var review in active)
        {
            var permissions = assignments.Where(r => r.UserId == review.ReviewerId &&
                    r.BranchId == review.BranchId && r.DepartmentId == review.DepartmentId)
                .SelectMany(r => r.RoleId == changedRoleId ? changedPermissions! :
                    r.Role.Permissions.Select(p => p.Permission.Name)).ToHashSet();
            if (!permissions.Contains(Permissions.MessageView) ||
                !permissions.Contains(ReviewAssignmentRules.PermissionForLevel(review.Level)))
                throw new DomainRuleViolationException("These changes would prevent a reviewer from completing an active review.");
        }
    }

    private async Task InTransactionAsync(Func<Task> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) { await action(); return; }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Retry attempts must reload state rather than reuse tracked mutations.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await action();
            await transaction.CommitAsync(ct);
        });
    }

    private void AddAudit(int? userId, int? roleId, object before, object after) => db.AccessAuditEvents.Add(new AccessAuditEvent
    {
        ActorId = current.UserId, TargetUserId = userId, RoleId = roleId, Timestamp = clock.UtcNow,
        BeforeJson = JsonSerializer.Serialize(before, JsonOptions), AfterJson = JsonSerializer.Serialize(after, JsonOptions),
        CorrelationId = correlation.CorrelationId
    });
    private static (int, int, int) Key(UserRole r) => (r.BranchId, r.DepartmentId, r.RoleId);
    private static ScopedRoleAssignmentDto[] AssignmentSnapshot(IEnumerable<UserRole> roles) =>
        roles.GroupBy(r => (r.BranchId, r.DepartmentId)).OrderBy(g => g.Key.BranchId).ThenBy(g => g.Key.DepartmentId)
            .Select(g => new ScopedRoleAssignmentDto(g.Key.BranchId, g.Key.DepartmentId,
                g.Select(r => r.RoleId).Order().ToArray())).ToArray();
}
