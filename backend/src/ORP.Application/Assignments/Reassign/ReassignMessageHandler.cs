using ORP.Application.Abstractions;
using ORP.Application.Assignments.Assign;

using ORP.Domain.Common;

namespace ORP.Application.Assignments.Reassign;

public sealed class ReassignMessageHandler(AssignMessageHandler assign, IORPStore store)
{
    public async Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
    {
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        if (message.CurrentAssigneeId is null) throw new DomainRuleViolationException("An unassigned message must be assigned before it can be reassigned.");
        await assign.HandleAsync(messageId, request, cancellationToken);
    }
}
