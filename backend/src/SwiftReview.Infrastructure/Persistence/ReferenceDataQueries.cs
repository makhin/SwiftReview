using Microsoft.EntityFrameworkCore;
using SwiftReview.Application.Abstractions;

namespace SwiftReview.Infrastructure.Persistence;

public sealed class ReferenceDataQueries(SwiftReviewDbContext db) : IReferenceDataQueries
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(UserAccess access, CancellationToken ct)
    {
        var workflows = await db.WorkflowDefinitions.AsNoTracking().Include(x => x.Steps)
            .Where(x => access.DepartmentIds.Contains(x.DepartmentId) && (x.BranchId == null || access.BranchIds.Contains(x.BranchId.Value)))
            .OrderBy(x => x.MessageType).ToListAsync(ct);
        return workflows.Select(x => new WorkflowSummaryDto(x.Id, x.Name, x.MessageType, x.DepartmentId, x.BranchId, x.IsActive,
            x.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto(s.Order, s.ReviewLevel, s.Required)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(UserAccess access, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Include(x => x.Branches).Include(x => x.Departments)
            .Where(x => x.Branches.Any(b => access.BranchIds.Contains(b.BranchId)) && x.Departments.Any(d => access.DepartmentIds.Contains(d.DepartmentId)))
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

    public async Task<IReadOnlyList<ReferenceItemDto>> GetDepartmentsAsync(UserAccess access, CancellationToken ct) =>
        await db.Departments.AsNoTracking()
            .Where(x => access.DepartmentIds.Contains(x.Id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ReferenceItemDto(x.Id, x.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetMessageTypesAsync(UserAccess access, CancellationToken ct) =>
        await db.Messages.AsNoTracking()
            .Where(x => access.BranchIds.Contains(x.BranchId) && access.DepartmentIds.Contains(x.OwningDepartmentId))
            .Select(x => x.MessageType)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
}
