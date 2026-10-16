using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence;

public sealed class ReferenceDataQueries(ORPDbContext db) : IReferenceDataQueries
{
    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(UserAccess access, CancellationToken ct)
    {
        var workflows = await db.WorkflowDefinitions.AsNoTracking().Include(x => x.Steps)
            .Where(x => access.IsGlobalAdministrator || db.DepartmentRoutingRules.Any(rule => rule.IsActive &&
                rule.WorkflowDefinitionId == x.Id && db.UserRoles.Any(role => role.UserId == access.UserId &&
                    rule.DepartmentId == role.DepartmentId && (rule.BranchId == null || role.BranchId == rule.BranchId) &&
                    role.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView))))
            .OrderBy(x => x.Code).ToListAsync(ct);
        return workflows.Select(x => new WorkflowSummaryDto(x.Id, x.Code, x.Name, x.IsActive,
            x.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto(s.Order, s.ReviewLevel, s.Required)).ToList())
        {
            ConfirmationMode = x.ConfirmationMode, ConfirmationActorMode = x.ConfirmationActorMode,
            AllowReviewerReuse = x.AllowReviewerReuse, AssignmentRetentionMode = x.AssignmentRetentionMode,
            RejectAllowed = x.RejectAllowed, UndoApprovalMode = x.UndoApprovalMode,
            UndoActorMode = x.UndoActorMode, UndoActiveReviewMode = x.UndoActiveReviewMode
        }).ToList();
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(UserAccess access, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().Include(x => x.Roles)
            .Where(user => access.IsGlobalAdministrator || user.Roles.Any(target => db.UserRoles.Any(actor =>
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
        await db.Branches.AsNoTracking().Where(x => access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                role.BranchId == x.Id && role.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new ReferenceItemDto(x.Id, x.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<ReferenceItemDto>> GetDepartmentsAsync(UserAccess access, CancellationToken ct) =>
        await db.Departments.AsNoTracking().Where(x => access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                role.DepartmentId == x.Id && role.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new ReferenceItemDto(x.Id, x.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetMessageTypesAsync(UserAccess access, CancellationToken ct) =>
        await db.ReadAccessibleMessages(access.UserId).Select(x => x.MessageType).Distinct().OrderBy(x => x).ToListAsync(ct);
}
