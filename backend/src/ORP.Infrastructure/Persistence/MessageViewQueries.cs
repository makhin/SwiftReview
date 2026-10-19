using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Messages.Get;

namespace ORP.Infrastructure.Persistence;

public sealed class MessageViewQueries(ORPDbContext db, IMessageQueries messages) : IMessageViewQueries
{
    public async Task<MessageViewData?> GetViewDataAsync(MessageDetailsDto message, UserAccess access, CancellationToken ct)
    {
        // Reuse the scoped audit access check and its existing detail filtering. This internal
        // request intentionally reads the full history; the public paged endpoint is unchanged.
        var audit = await messages.AuditAsync(message.Id, new AuditTrailRequest(0, int.MaxValue), access, ct);
        if (audit is null) return null;
        var workflow = await db.WorkflowDefinitions.AsNoTracking().Include(w => w.Steps)
            .SingleAsync(w => db.Messages.Any(m => m.Id == message.Id && m.WorkflowDefinitionId == w.Id), ct);
        var reviews = await db.Reviews.AsNoTracking().Where(r => r.MessageId == message.Id).ToListAsync(ct);
        var userIds = reviews.Select(r => r.ReviewerId)
            .Concat(audit.Items.Where(a => a.Actor != null).Select(a => a.Actor!.UserId))
            .Concat(audit.Items.Where(a => a.Details.AssigneeId != null).Select(a => a.Details.AssigneeId!.Value))
            .Concat(audit.Items.Where(a => a.Details.PreviousAssigneeId != null).Select(a => a.Details.PreviousAssigneeId!.Value))
            .Concat(message.CurrentAssigneeId is { } id ? [id] : Array.Empty<int>()).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName }).ToListAsync(ct);
        var departments = message.DepartmentIds.ToArray();
        var roles = await db.UserRoles.AsNoTracking().Where(r => userIds.Contains(r.UserId) &&
                r.BranchId == message.BranchId && departments.Contains(r.DepartmentId))
            .Select(r => new { r.UserId, r.Role.Name }).ToListAsync(ct);
        var roleNames = roles.GroupBy(r => r.UserId).ToDictionary(g => g.Key,
            g => string.Join(", ", g.Select(r => r.Name).Distinct().Order(StringComparer.Ordinal)));
        return new(workflow.RequiredLevels(), reviews, audit.Items,
            users.ToDictionary(u => u.Id, u => new MessageViewUser(u.DisplayName, roleNames.GetValueOrDefault(u.Id))));
    }
}
