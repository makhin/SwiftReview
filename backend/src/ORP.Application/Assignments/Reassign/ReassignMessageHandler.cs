using ORP.Application.Abstractions;
using ORP.Application.Assignments.Assign;

namespace ORP.Application.Assignments.Reassign;

public sealed class ReassignMessageHandler(AssignMessageHandler assign)
{
    public async Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
        => await assign.ReassignAsync(messageId, request, cancellationToken);
}
