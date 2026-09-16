using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Domain.Common;

namespace ORP.Application.Assignments.GetCandidates;

public sealed class GetAssignmentCandidatesHandler(IAssignmentCandidateQueries queries,
    ICurrentUser user, MessageAuthorizationService authorization)
{
    public async Task<IReadOnlyList<AssignmentCandidateDto>> HandleAsync(long messageId,
        CancellationToken cancellationToken)
    {
        var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, cancellationToken);
        var message = access.Message;
        var reviewLevel = ReviewAssignmentRules.AssignmentLevelForState(message.State)
            ?? throw new DomainRuleViolationException($"Assignment is not allowed while message is in state '{message.State}'.");
        var source = access.Source;
        var reviews = access.Reviews;
        return await queries.GetEligibleAsync(source.BranchId, source.DepartmentId, reviewLevel,
            ReviewAssignmentRules.ApprovedReviewerIds(reviews), user.UserId, message.CurrentAssigneeId,
            cancellationToken);
    }
}
