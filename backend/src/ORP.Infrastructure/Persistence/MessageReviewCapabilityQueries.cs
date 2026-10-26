using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Reviews;

namespace ORP.Infrastructure.Persistence;

internal static class MessageReviewCapabilityQueries
{
    // Batch only the current page. Use the same domain checks as mutations, without
    // loading the whole grid or issuing per-row permission/workflow queries.
    public static async Task<Dictionary<long, ReviewCapabilities>> ReadReviewCapabilitiesAsync(
        this ORPDbContext db, IReadOnlyDictionary<long, byte[]> versions, UserAccess access, CancellationToken ct)
    {
        if (versions.Count == 0) return [];
        var ids = versions.Keys.ToArray();
        var messages = await db.Messages.AsNoTracking().Include(x => x.Departments)
            .Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var workflowIds = messages.Select(x => x.WorkflowDefinitionId).Distinct().ToArray();
        var workflows = await db.WorkflowDefinitions.AsNoTracking().Include(x => x.Steps)
            .Where(x => workflowIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var reviews = (await db.Reviews.AsNoTracking().Where(x => ids.Contains(x.MessageId)).ToListAsync(ct))
            .ToLookup(x => x.MessageId);
        var selfAssignmentIds = messages.Where(x => x.CurrentAssigneeId is null &&
                !access.IsGlobalAdministrator && !workflows[x.WorkflowDefinitionId].AllowReviewerReuse)
            .Select(x => x.Id).ToArray();
        var assignments = (selfAssignmentIds.Length == 0 ? [] : await db.Assignments.AsNoTracking()
            .Where(x => selfAssignmentIds.Contains(x.MessageId) && x.EndedAt == null).ToListAsync(ct))
            .ToLookup(x => x.MessageId);
        return messages.ToDictionary(x => x.Id, x =>
            x.RowVersion.SequenceEqual(versions[x.Id])
                ? ReviewCapabilities.For(x, workflows[x.WorkflowDefinitionId], reviews[x.Id].ToArray(),
                    x.BranchId!.Value, access, assignments[x.Id].ToArray())
                : new ReviewCapabilities(workflows[x.WorkflowDefinitionId].ConfirmationMode, false, null, false));
    }
}
