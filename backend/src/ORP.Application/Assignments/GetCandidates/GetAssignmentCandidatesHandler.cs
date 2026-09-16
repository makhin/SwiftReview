using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Domain.Common;

namespace ORP.Application.Assignments.GetCandidates;

public sealed class GetAssignmentCandidatesHandler(IORPStore store, IAssignmentCandidateQueries queries,
    ICurrentUser user, MessageAuthorizationService authorization)
{
    public async Task<IReadOnlyList<AssignmentCandidateDto>> HandleAsync(long messageId,
        CancellationToken cancellationToken)
    {
        await authorization.RequireAsync(messageId, Permissions.MessageAssign, cancellationToken);
        var message = await store.FindMessageAsync(messageId, cancellationToken)
            ?? throw new ResourceNotFoundException("Message was not found.");
        var reviewLevel = ReviewAssignmentRules.AssignmentLevelForState(message.State)
            ?? throw new DomainRuleViolationException($"Assignment is not allowed while message is in state '{message.State}'.");
        var source = await store.FindMessageSourceAsync(messageId, cancellationToken)
            ?? throw new ResourceNotFoundException("SWIFT message was not found.");
        var reviews = await store.GetReviewsAsync(messageId, cancellationToken);
        return await queries.GetEligibleAsync(source.BranchId, source.DepartmentId, reviewLevel,
            ReviewAssignmentRules.ApprovedReviewerIds(reviews), user.UserId, message.CurrentAssigneeId,
            cancellationToken);
    }
}
