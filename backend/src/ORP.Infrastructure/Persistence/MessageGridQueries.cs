using Microsoft.EntityFrameworkCore;
using ORP.Application.Grids;
using ORP.Application.Abstractions;
using ORP.Application.Messages.GetStateCounts;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Infrastructure.Persistence;

public sealed class MessageGridQueries(ORPDbContext db)
{
    public async Task<IReadOnlyList<MessageStateCountDto>> StateCountsAsync(UserAccess access, CancellationToken ct)
    {
        var counts = await db.ReadAccessibleMessages(access.UserId)
            .GroupBy(x => new { x.State, x.MessageType })
            .Select(group => new { group.Key.State, group.Key.MessageType, Count = group.Count() })
            .ToListAsync(ct);
        return MessageStateCounts.FromTypeCounts(counts.Select(x => (x.State, x.MessageType, x.Count)));
    }

    public async Task<PagedResult<MessageGridRowDto>> LoadAsync(MessageGridRequest request, UserAccess access, CancellationToken ct)
    {
        var options = GridQuery<MessageReadRow>.Create(request, GridFields.Messages, 500, 5, new SortClause("receivedAt", "desc"));
        var assignmentScope = request.AssignmentScope;
        var query = db.ReadAccessibleMessages(access.UserId, assignmentScope == MessageAssignmentScopes.Assignable
            ? Permissions.MessageAssign : Permissions.MessageView);
        query = assignmentScope switch
        {
            null => query,
            MessageAssignmentScopes.Mine => query.Where(x =>
                x.CurrentAssigneeId == access.UserId || x.ActiveReviewerId == access.UserId),
            MessageAssignmentScopes.Departments => query.Where(x => access.IsGlobalAdministrator || x.CurrentAssigneeId != null),
            MessageAssignmentScopes.Assignable => query.Where(x =>
                x.State == MessageState.New || x.State == MessageState.Assigned ||
                x.State == MessageState.WaitingForSecondReview ||
                x.State == MessageState.WaitingForThirdReview),
            _ => throw new FormatException("Unsupported message assignment scope.")
        };
        query = options.Filter(query);
        var count = await query.CountAsync(ct);
        var undoCandidates = db.ReadUndoCandidates(access);
        var rows = options.Page(query)
            .Select(x => new MessageGridRowDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                MessageType = x.MessageType,
                Direction = x.Direction,
                BranchId = x.BranchId,
                DepartmentId = x.DepartmentId,
                State = x.State,
                ReceivedAt = x.ReceivedAt,
                CurrentAssigneeId = x.CurrentAssigneeId,
                ActiveReviewId = x.ActiveReviewId,
                ActiveReviewLevel = x.ActiveReviewLevel,
                ActiveReviewerId = x.ActiveReviewerId,
                WorkflowDefinitionId = x.WorkflowDefinitionId,
                CanReview = (x.State == MessageState.Assigned || x.State == MessageState.FirstReviewInProgress ||
                    x.State == MessageState.WaitingForSecondReview || x.State == MessageState.SecondReviewInProgress ||
                    x.State == MessageState.WaitingForThirdReview || x.State == MessageState.ThirdReviewInProgress) &&
                    (access.IsGlobalAdministrator || (
                        (x.ActiveReviewId != null ? x.ActiveReviewerId == access.UserId : x.CurrentAssigneeId == access.UserId) &&
                        !db.Reviews.Any(r => r.MessageId == x.Id && r.ReviewerId == access.UserId && r.Status == ReviewStatus.Approved) &&
                        db.UserRoles.Any(role => role.UserId == access.UserId && role.BranchId == x.BranchId && role.DepartmentId == x.DepartmentId &&
                            role.Role.Permissions.Any(grant => grant.Permission.Name ==
                                (x.State == MessageState.Assigned || x.State == MessageState.FirstReviewInProgress ? Permissions.ReviewLevel1 :
                                 x.State == MessageState.WaitingForSecondReview || x.State == MessageState.SecondReviewInProgress ? Permissions.ReviewLevel2 : Permissions.ReviewLevel3))))),
                CanChangeWorkflow = (x.State == MessageState.New || x.State == MessageState.Assigned) &&
                    !db.Reviews.Any(review => review.MessageId == x.Id && review.Status != ReviewStatus.Cancelled && review.Status != ReviewStatus.Undone) &&
                    (access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                        role.BranchId == x.BranchId && role.DepartmentId == x.DepartmentId &&
                        role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.WorkflowManage))),
                UndoReviewId = undoCandidates.Where(candidate => candidate.MessageId == x.Id)
                    .Select(candidate => (long?)candidate.ReviewId).FirstOrDefault(),
                RequiredReviewLevels = db.WorkflowSteps
                    .Where(step => step.WorkflowDefinitionId == x.WorkflowDefinitionId && step.Required)
                    .OrderBy(step => step.Order)
                    .Select(step => step.ReviewLevel).ToArray()
            });
        return new(await rows.ToListAsync(ct), count);
    }
}
