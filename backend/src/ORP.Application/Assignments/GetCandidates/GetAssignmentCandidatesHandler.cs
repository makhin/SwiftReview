using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Domain.Common;

namespace ORP.Application.Assignments.GetCandidates;

public sealed class GetAssignmentCandidatesHandler(IAssignmentCandidateQueries queries, IORPStore store,
    MessageAuthorizationService authorization)
{
    public async Task<IReadOnlyList<AssignmentCandidateDto>> HandleAsync(long messageId,
        CancellationToken cancellationToken, int? level = null)
    {
        var access = await authorization.RequireAsync(messageId, Permissions.MessageAssign, cancellationToken);
        var message = access.Message;
        var workflow = await store.FindWorkflowAsync(message.WorkflowDefinitionId, cancellationToken)
            ?? throw new ResourceNotFoundException("Workflow was not found.");
        var reviewLevel = ReviewAssignmentRules.RequireAssignableLevel(message, workflow, access.Reviews, level);
        var assignments = await store.GetActiveAssignmentsAsync(message.Id, cancellationToken);
        var source = access.Source;
        var reviews = access.Reviews;
        return await queries.GetEligibleAsync(source.BranchId, message.ProcessingDepartmentIds, reviewLevel,
            ReviewAssignmentRules.ExcludedReviewerIds(workflow, reviews, assignments, reviewLevel),
            assignments.SingleOrDefault(a => a.Level == reviewLevel && a.EndedAt is null)?.AssignedTo ??
                (reviewLevel == ReviewAssignmentRules.CurrentLevel(message.State) ? message.CurrentAssigneeId : null),
            cancellationToken);
    }
}
