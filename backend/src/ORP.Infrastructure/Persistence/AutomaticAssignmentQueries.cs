using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Domain.Identity;
using ORP.Domain.Messages;

namespace ORP.Infrastructure.Persistence;

public sealed class AutomaticAssignmentQueries(ORPDbContext db) : IAutomaticAssignmentQueries
{
    private static readonly MessageState[] ActiveStates =
    [
        MessageState.Assigned,
        MessageState.FirstReviewInProgress,
        MessageState.WaitingForSecondReview,
        MessageState.SecondReviewInProgress,
        MessageState.WaitingForThirdReview,
        MessageState.ThirdReviewInProgress
    ];

    public async Task<int?> SelectAssigneeAsync(long messageId, int branchId, int departmentId,
        int reviewLevel, IReadOnlyCollection<int> excludedUserIds, CancellationToken cancellationToken)
    {
        var reviewPermission = ReviewAssignmentRules.PermissionForLevel(reviewLevel);
        var excluded = excludedUserIds.ToArray();
        return await db.Users.AsNoTracking()
            .Where(user => !excluded.Contains(user.Id) &&
                user.Branches.Any(branch => branch.BranchId == branchId) &&
                (user.Departments.Any(department => department.DepartmentId == departmentId) ||
                    user.Roles.Any(userRole => userRole.Role.Permissions.Any(rolePermission =>
                        rolePermission.Permission.Name == Permissions.MessageAccessAllDepartments))) &&
                user.Roles.Any(userRole => userRole.Role.Permissions.Any(rolePermission =>
                    rolePermission.Permission.Name == Permissions.MessageView)) &&
                user.Roles.Any(userRole => userRole.Role.Permissions.Any(rolePermission =>
                    rolePermission.Permission.Name == reviewPermission)))
            .Select(user => new
            {
                user.Id,
                Load = db.Messages.Count(message => message.Id != messageId &&
                    message.CurrentAssigneeId == user.Id && ActiveStates.Contains(message.State))
            })
            .OrderBy(candidate => candidate.Load)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => (int?)candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnassignedMessageCursor>> GetUnassignedMessagesAsync(
        UnassignedMessageCursor? after, int take, CancellationToken cancellationToken) =>
        await (from message in db.Messages.AsNoTracking()
               join source in db.SwiftMessageSource.AsNoTracking() on message.Id equals source.MessageId
               where message.State == MessageState.New && message.CurrentAssigneeId == null &&
                   (after == null || source.ReceivedAt > after.ReceivedAt ||
                       source.ReceivedAt == after.ReceivedAt && message.Id > after.MessageId)
               orderby source.ReceivedAt, message.Id
               select new UnassignedMessageCursor(source.ReceivedAt, message.Id))
            .Take(take)
            .ToListAsync(cancellationToken);
}
