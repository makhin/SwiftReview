using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence;

public sealed class ReferenceDataQueries(ORPDbContext db) : IReferenceDataQueries
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(UserAccess access, CancellationToken ct)
    {
        var workflows = await db.WorkflowDefinitions.AsNoTracking().Include(x => x.Steps)
            .Where(x => db.UserRoles.Any(role => role.UserId == access.UserId &&
                role.DepartmentId == x.DepartmentId && (x.BranchId == null || role.BranchId == x.BranchId) &&
                role.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)))
            .OrderBy(x => x.MessageType).ToListAsync(ct);
        return workflows.Select(x => new WorkflowSummaryDto(x.Id, x.Name, x.MessageType, x.DepartmentId, x.BranchId, x.IsActive,
            x.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto(s.Order, s.ReviewLevel, s.Required)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(UserAccess access, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Include(x => x.Roles)
            .Where(user => user.Roles.Any(target => db.UserRoles.Any(actor =>
                actor.UserId == access.UserId && actor.BranchId == target.BranchId &&
                actor.DepartmentId == target.DepartmentId &&
                actor.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView))))
            .OrderBy(x => x.DisplayName).ToListAsync(ct);
        return users.Select(x =>
        {
            var visibleRoles = x.Roles.Where(r => access.CanAccess(r.BranchId, r.DepartmentId)).ToArray();
            return new UserSummaryDto(x.Id, x.UserName, x.DisplayName,
                visibleRoles.Select(r => r.BranchId).Distinct().Order().ToArray(),
                visibleRoles.Select(r => r.DepartmentId).Distinct().Order().ToArray());
        }).ToList();
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetBranchesAsync(UserAccess access, CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(x => db.UserRoles.Any(role => role.UserId == access.UserId &&
                role.BranchId == x.Id && role.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new ReferenceItemDto(x.Id, x.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<ReferenceItemDto>> GetDepartmentsAsync(UserAccess access, CancellationToken ct) =>
        await db.Departments.AsNoTracking().Where(x => db.UserRoles.Any(role => role.UserId == access.UserId &&
                role.DepartmentId == x.Id && role.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new ReferenceItemDto(x.Id, x.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetMessageTypesAsync(UserAccess access, CancellationToken ct) =>
        await db.ReadAccessibleMessages(access.UserId).Select(x => x.MessageType).Distinct().OrderBy(x => x).ToListAsync(ct);
}
