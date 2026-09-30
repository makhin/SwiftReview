using FluentValidation;
using ORP.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Workflows;

namespace ORP.Infrastructure.Persistence;

public sealed class WorkflowResolver(ORPDbContext db) : IWorkflowResolver
{
    public async Task<WorkflowDefinition> ResolveAsync(string messageType, MessageDirection direction, int departmentId, int branchId,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(direction)) throw new ValidationException("A valid message direction is required to select a workflow.");
        var candidates = await db.WorkflowDefinitions.Include(x => x.Steps)
            .Where(x => x.IsActive && x.MessageType == messageType && x.Direction == direction && x.DepartmentId == departmentId &&
                (x.BranchId == branchId || x.BranchId == null))
            .OrderByDescending(x => x.BranchId == branchId)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            throw new ResourceNotFoundException("No active workflow matches the message type, direction, department and branch.");

        var selectedScope = candidates[0].BranchId;
        if (candidates.Count(x => x.BranchId == selectedScope) != 1)
            throw new ValidationException("More than one active workflow matches the same message scope.");

        _ = candidates[0].RequiredLevels();
        return candidates[0];
    }
}
