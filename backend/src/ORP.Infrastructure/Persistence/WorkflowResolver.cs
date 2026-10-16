using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Importer;

namespace ORP.Infrastructure.Persistence;

public sealed class WorkflowResolver(ORPDbContext db) : IWorkflowResolver
{
    public async Task<WorkflowResolution> ResolveAsync(string messageType, MessageDirection direction,
        IReadOnlyList<int> departmentIds, int branchId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(direction)) throw new FormatException("A valid message direction is required.");
        var rules = await db.DepartmentRoutingRules.AsNoTracking().Where(x => x.IsActive &&
            x.Direction == (int)direction && (x.BranchId == null || x.BranchId == branchId))
            .Select(x => new SaaDepartmentRule
            {
                Id = x.Id, DepartmentId = x.DepartmentId, BranchId = x.BranchId,
                Direction = (SaaMessageDirection)x.Direction, MatchKind = (SaaDepartmentMatchKind)x.MatchKind,
                MessageTypePattern = x.MessageTypePattern, WorkflowDefinitionId = x.WorkflowDefinitionId, IsActive = x.IsActive
            }).ToListAsync(cancellationToken);
        var selected = SaaDepartmentRuleResolver.SelectRules(rules, branchId, messageType, (SaaMessageDirection)direction);
        if (selected.Length == 0) throw new FormatException("No active department rule matches the message.");
        var visible = selected.Select(x => x.DepartmentId).Distinct().Order().ToArray();
        if (!visible.SequenceEqual(departmentIds.Order()))
            throw new FormatException("Department routing changed. Reload routing configuration before importing.");
        if (selected.Any(x => x.WorkflowDefinitionId is null))
            throw new FormatException("A selected routing rule has no workflow. Apply workflow routing configuration.");
        var ids = selected.Select(x => x.WorkflowDefinitionId!.Value).Distinct().ToArray();
        var workflows = await db.WorkflowDefinitions.Include(x => x.Steps)
            .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (ids.Any(id => !workflows.ContainsKey(id) || !workflows[id].IsActive))
            throw new FormatException("A selected routing workflow is missing or inactive.");
        foreach (var workflow in workflows.Values) _ = workflow.RequiredLevels();
        var processing = selected.Where(x => workflows[x.WorkflowDefinitionId!.Value].ConfirmationMode != ConfirmationMode.None).ToArray();
        var chosen = (processing.Length > 0 ? processing : selected).Select(x => x.WorkflowDefinitionId!.Value).Distinct().ToArray();
        if (chosen.Length != 1)
            throw new FormatException("Department routing rules require incompatible workflows.");
        return new WorkflowResolution(workflows[chosen[0]], visible,
            processing.Select(x => x.DepartmentId).Distinct().Order().ToArray());
    }
}
