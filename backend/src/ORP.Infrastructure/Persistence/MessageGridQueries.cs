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
    public async Task<MessageStateCountsDto> StateCountsAsync(UserAccess access, CancellationToken ct)
    {
        var counts = await db.ReadAccessibleMessages(access.UserId)
            .GroupBy(x => new { x.State, x.MessageType })
            .Select(group => new { group.Key.State, group.Key.MessageType, Count = group.Count() })
            .ToListAsync(ct);
        return MessageStateCounts.FromTypeCounts(counts.Select(x => (x.State, x.MessageType, x.Count)));
    }

    public async Task<PagedResult<MessageGridRowDto>> LoadAsync(MessageGridRequest request, UserAccess access, CancellationToken ct)
    {
        var options = GridQuery<MessageReadRow>.Create(MessageGridOptions.Create(request), GridFields.Messages, 500, 5, new SortClause("receivedAt", "desc"));
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
        if (request.Departments is { Count: > 0 })
            query = query.Where(x => db.MessageDepartments.Any(d => d.MessageId == x.Id &&
                request.Departments.Contains(d.DepartmentId) &&
                (access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                    role.BranchId == x.BranchId && role.DepartmentId == d.DepartmentId &&
                    role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.MessageView)))));
        query = options.Filter(query);
        var count = await query.CountAsync(ct);
        var undoCandidates = db.ReadUndoCandidates(access);
        var rows = options.Page(query)
            .Select(x => new MessageGridRowDto
            {
                Id = x.Id,
                Version = Convert.ToBase64String(x.RowVersion),
                ExternalId = x.ExternalId,
                Sender = x.Sender,
                Receiver = x.Receiver,
                MessageType = x.MessageType,
                Direction = x.Direction,
                BranchId = x.BranchId,
                BranchName = x.BranchName,
                DepartmentNames = string.Join(", ", x.DepartmentNames),
                CurrentAssigneeName = x.CurrentAssigneeName,
                ActiveReviewerName = x.ActiveReviewerName,
                WorkflowDefinitionName = x.WorkflowDefinitionName,
                DepartmentIds = x.DepartmentIds,
                State = x.State,
                ReceivedAt = x.ReceivedAt,
                CurrentAssigneeId = x.CurrentAssigneeId,
                ActiveReviewId = x.ActiveReviewId,
                ActiveReviewLevel = x.ActiveReviewLevel,
                ActiveReviewerId = x.ActiveReviewerId,
                WorkflowDefinitionId = x.WorkflowDefinitionId,
                CanChangeWorkflow = (x.State == MessageState.New || x.State == MessageState.Assigned) &&
                    !db.Reviews.Any(review => review.MessageId == x.Id && review.Status != ReviewStatus.Cancelled && review.Status != ReviewStatus.Undone) &&
                    (access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                        role.BranchId == x.BranchId && db.MessageDepartments.Any(d => d.MessageId == x.Id &&
                            d.DepartmentId == role.DepartmentId) &&
                        db.UserRoles.Any(view => view.UserId == access.UserId && view.BranchId == x.BranchId &&
                            view.DepartmentId == role.DepartmentId && view.Role.Permissions.Any(grant =>
                                grant.Permission.Name == Permissions.MessageView)) &&
                        role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.WorkflowManage))),
                UndoReviewId = undoCandidates.Where(candidate => candidate.MessageId == x.Id)
                    .Select(candidate => (long?)candidate.ReviewId).FirstOrDefault(),
                RequiredReviewLevels = db.WorkflowSteps
                    .Where(step => step.WorkflowDefinitionId == x.WorkflowDefinitionId && step.Required)
                    .OrderBy(step => step.Order)
                    .Select(step => step.ReviewLevel).ToArray()
            });
        var items = await rows.ToListAsync(ct);
        var capabilities = await db.ReadReviewCapabilitiesAsync(
            items.ToDictionary(x => x.Id, x => Convert.FromBase64String(x.Version)), access, ct);
        foreach (var item in items)
        {
            if (!capabilities.TryGetValue(item.Id, out var actions)) continue;
            item.ConfirmationMode = actions.ConfirmationMode;
            item.CanConfirm = actions.CanConfirm;
            item.ConfirmationLevel = actions.ConfirmationLevel;
            item.CanReview = actions.CanReview;
        }
        return new(items, count);
    }
}
