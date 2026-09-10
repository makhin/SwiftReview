using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Identity;

namespace ORP.Infrastructure.Persistence;

public sealed class AssignmentCandidateQueries(ORPDbContext db) : IAssignmentCandidateQueries
{
    public async Task<IReadOnlyList<AssignmentCandidateDto>> GetEligibleAsync(int branchId,
        int departmentId, int reviewLevel, IReadOnlyCollection<int> excludedUserIds, int actorId,
        int? currentAssigneeId, CancellationToken cancellationToken)
    {
        var reviewPermission = ReviewAssignmentRules.PermissionForLevel(reviewLevel);
        var excluded = excludedUserIds.Append(actorId)
            .Concat(currentAssigneeId is null ? [] : [currentAssigneeId.Value])
            .Distinct().ToArray();
        return await db.Users.AsNoTracking()
            .Where(user => !excluded.Contains(user.Id) &&
                user.Roles.Any(r => r.BranchId == branchId && r.DepartmentId == departmentId &&
                    r.Role.Permissions.Any(p => p.Permission.Name == Permissions.MessageView)) &&
                user.Roles.Any(r => r.BranchId == branchId && r.DepartmentId == departmentId &&
                    r.Role.Permissions.Any(p => p.Permission.Name == reviewPermission)))
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Select(user => new AssignmentCandidateDto(user.Id, user.UserName, user.DisplayName))
            .ToListAsync(cancellationToken);
    }
}
