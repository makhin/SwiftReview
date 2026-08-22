using SwiftReview.Application.Abstractions;
using SwiftReview.Application.Assignments.Assign;

using SwiftReview.Domain.Common;

namespace SwiftReview.Application.Assignments.Reassign;

public sealed class ReassignMessageHandler(AssignMessageHandler assign, ISwiftReviewStore store)
{
    public async Task HandleAsync(long messageId, AssignMessageRequest request, CancellationToken cancellationToken)
    {
        var message = await store.FindMessageAsync(messageId, cancellationToken) ?? throw new ResourceNotFoundException("Message was not found.");
        if (message.CurrentAssigneeId is null) throw new DomainRuleViolationException("An unassigned message must be assigned before it can be reassigned.");
        await assign.HandleAsync(messageId, request, cancellationToken);
    }
}
