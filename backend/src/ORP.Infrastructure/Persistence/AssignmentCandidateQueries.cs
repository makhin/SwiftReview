using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence;

public sealed class AssignmentCandidateQueries(ORPDbContext db) : IAssignmentCandidateQueries
{
    public async Task<IReadOnlyList<AssignmentCandidateDto>> GetEligibleAsync(int branchId,
        IReadOnlyList<int> departmentIds, int reviewLevel, IReadOnlyCollection<int> excludedUserIds, int actorId,
        int? currentAssigneeId, bool allowSelfAssignment, CancellationToken cancellationToken)
    {
        var reviewPermission = ReviewAssignmentRules.PermissionForLevel(reviewLevel);
        var excluded = excludedUserIds.Concat(allowSelfAssignment ? Array.Empty<int>() : [actorId])
            .Concat(currentAssigneeId is null ? [] : [currentAssigneeId.Value])
            .Distinct().ToArray();
        return await db.Users.AsNoTracking()
            .Where(user => user.Id != currentAssigneeId && (user.IsGlobalAdministrator || (!excluded.Contains(user.Id) &&
                user.Roles.Any(r => r.BranchId == branchId && departmentIds.Contains(r.DepartmentId) &&
                    user.Roles.Any(view => view.BranchId == branchId && view.DepartmentId == r.DepartmentId &&
                        view.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)) &&
                    r.Role.Permissions.Any(p => p.Permission.Name == reviewPermission)))))
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Select(user => new AssignmentCandidateDto(user.Id, user.UserName, user.DisplayName))
            .ToListAsync(cancellationToken);
    }
}
