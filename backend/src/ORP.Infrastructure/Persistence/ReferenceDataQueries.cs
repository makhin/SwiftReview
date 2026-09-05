using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence;

public sealed class ReferenceDataQueries(ORPDbContext db) : IReferenceDataQueries
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(UserAccess access, CancellationToken ct)
    {
        var allDepartments = access.HasAllDepartmentAccess;
        var workflows = await db.WorkflowDefinitions.AsNoTracking().Include(x => x.Steps)
            .Where(x => (allDepartments || access.DepartmentIds.Contains(x.DepartmentId)) &&
                (x.BranchId == null || access.BranchIds.Contains(x.BranchId.Value)))
            .OrderBy(x => x.MessageType).ToListAsync(ct);
        return workflows.Select(x => new WorkflowSummaryDto(x.Id, x.Name, x.MessageType, x.DepartmentId, x.BranchId, x.IsActive,
            x.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto(s.Order, s.ReviewLevel, s.Required)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(UserAccess access, CancellationToken ct)
    {
        var allDepartments = access.HasAllDepartmentAccess;
        var users = await db.Users.AsNoTracking().Include(x => x.Branches).Include(x => x.Departments)
            .Where(x => x.Branches.Any(b => access.BranchIds.Contains(b.BranchId)) &&
                (allDepartments || x.Departments.Any(d => access.DepartmentIds.Contains(d.DepartmentId)) ||
                    x.Roles.Any(userRole => userRole.Role.Permissions.Any(rolePermission =>
                        rolePermission.Permission.Name == Permissions.MessageAccessAllDepartments))))
            .OrderBy(x => x.DisplayName).ToListAsync(ct);
        return users.Select(x => new UserSummaryDto(x.Id, x.UserName, x.DisplayName,
            x.Branches.Select(b => b.BranchId).Order().ToList(), x.Departments.Select(d => d.DepartmentId).Order().ToList())).ToList();
    }

    public async Task<IReadOnlyList<ReferenceItemDto>> GetBranchesAsync(UserAccess access, CancellationToken ct) =>
        await db.Branches.AsNoTracking()
            .Where(x => access.BranchIds.Contains(x.Id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ReferenceItemDto(x.Id, x.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReferenceItemDto>> GetDepartmentsAsync(UserAccess access, CancellationToken ct)
    {
        var allDepartments = access.HasAllDepartmentAccess;
        return await db.Departments.AsNoTracking()
            .Where(x => allDepartments || access.DepartmentIds.Contains(x.Id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ReferenceItemDto(x.Id, x.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetMessageTypesAsync(UserAccess access, CancellationToken ct)
    {
        var allDepartments = access.HasAllDepartmentAccess;
        return await db.ReadMessages()
            .Where(x => access.BranchIds.Contains(x.BranchId) &&
                (allDepartments || access.DepartmentIds.Contains(x.DepartmentId)))
            .Select(x => x.MessageType)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }
}
