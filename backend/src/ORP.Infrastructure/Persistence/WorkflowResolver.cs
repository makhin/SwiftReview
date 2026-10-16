using FluentValidation;
using ORP.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence;

public sealed class WorkflowResolver(ORPDbContext db) : IWorkflowResolver
{
    public async Task<WorkflowDefinition> ResolveAsync(string messageType, MessageDirection direction, IReadOnlyList<int> departmentIds, int branchId,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(direction)) throw new ValidationException("A valid message direction is required to select a workflow.");
        var candidates = await db.WorkflowDefinitions.Include(x => x.Steps).Include(x => x.Departments)
            .Where(x => x.IsActive && x.Direction == direction &&
                (x.BranchId == branchId || x.BranchId == null))
            .ToListAsync(cancellationToken);
        var preferred = WorkflowMatching.GetPreferred(candidates, messageType, direction, departmentIds, branchId);

        if (preferred.Length == 0)
            throw new ResourceNotFoundException("No active workflow matches the message type, direction, departments and branch.");

        if (preferred.Length != 1)
            throw new ValidationException("More than one active workflow matches the same message scope.");

        _ = preferred[0].RequiredLevels();
        return preferred[0];
    }
}
