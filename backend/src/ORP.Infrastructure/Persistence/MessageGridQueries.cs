using System.Collections;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
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

    public async Task<LoadResult> LoadAsync(DataSourceLoadOptionsBase options, UserAccess access,
        string? assignmentScope, CancellationToken ct, IReadOnlyList<int>? departments = null)
    {
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
        if (departments is { Count: > 0 })
            query = query.Where(x => db.MessageDepartments.Any(d => d.MessageId == x.Id &&
                departments.Contains(d.DepartmentId) &&
                (access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                    role.BranchId == x.BranchId && role.DepartmentId == d.DepartmentId &&
                    role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.MessageView)))));
        var undoCandidates = db.ReadUndoCandidates(access);
        var rows = query
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
                RejectAllowed = db.WorkflowDefinitions.Where(workflow => workflow.Id == x.WorkflowDefinitionId)
                    .Select(workflow => workflow.RejectAllowed).First(),
                CanAssign = db.WorkflowDefinitions.Any(workflow => workflow.Id == x.WorkflowDefinitionId && workflow.IsActive) &&
                    db.WorkflowSteps.Any(step => step.WorkflowDefinitionId == x.WorkflowDefinitionId && step.Required &&
                        step.ReviewLevel == (x.State == MessageState.New || x.State == MessageState.Assigned ? 1 :
                            x.State == MessageState.WaitingForSecondReview ? 2 : 3)) && (x.State == MessageState.New || x.State == MessageState.Assigned ||
                    x.State == MessageState.WaitingForSecondReview || x.State == MessageState.WaitingForThirdReview) &&
                    (access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId && role.BranchId == x.BranchId &&
                        db.MessageDepartments.Any(d => d.MessageId == x.Id && d.DepartmentId == role.DepartmentId && d.CanProcess) &&
                        db.UserRoles.Any(view => view.UserId == access.UserId && view.BranchId == x.BranchId && view.DepartmentId == role.DepartmentId &&
                            view.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.MessageView)) &&
                        role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.MessageAssign))),
                CanViewAudit = access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId && role.BranchId == x.BranchId &&
                    db.MessageDepartments.Any(d => d.MessageId == x.Id && d.DepartmentId == role.DepartmentId) &&
                    db.UserRoles.Any(view => view.UserId == access.UserId && view.BranchId == x.BranchId && view.DepartmentId == role.DepartmentId &&
                        view.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.MessageView)) &&
                    role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.AuditView)),
                CanChangeWorkflow = (x.State == MessageState.New || x.State == MessageState.Assigned) &&
                    !db.Reviews.Any(review => review.MessageId == x.Id && review.Status != ReviewStatus.Cancelled && review.Status != ReviewStatus.Undone) &&
                    (access.IsGlobalAdministrator || db.UserRoles.Any(role => role.UserId == access.UserId &&
                        role.BranchId == x.BranchId && db.MessageDepartments.Any(d => d.MessageId == x.Id &&
                            d.DepartmentId == role.DepartmentId && d.CanProcess) &&
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
        // Hydrate actions before applying select; keep paging, grouping and counts in SQL.
        var select = options.Select;
        var preSelect = options.PreSelect;
        options.Select = null;
        options.PreSelect = null;
        LoadResult result;
        try { result = await DataSourceLoader.LoadAsync(rows, options, ct); }
        finally { options.Select = select; options.PreSelect = preSelect; }
        var items = LeafRows(result.data).ToArray();
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
        if (select is { Length: > 0 } && result.data is not null)
            result.data = SelectLeaves(result.data, select);
        return result;
    }
    private static IEnumerable<MessageGridRowDto> LeafRows(IEnumerable? data)
    {
        if (data is null) yield break;
        foreach (var item in data)
        {
            if (item is MessageGridRowDto row) yield return row;
            else if (item is Group group)
                foreach (var leaf in LeafRows(group.items)) yield return leaf;
        }
    }

    private static IEnumerable SelectLeaves(IEnumerable data, string[] select)
    {
        var values = data.Cast<object>().ToArray();
        if (values.FirstOrDefault() is Group)
        {
            foreach (var group in values.Cast<Group>())
                if (group.items is not null) group.items = SelectLeaves(group.items, select).Cast<object>().ToArray();
            return values;
        }
        return DataSourceLoader.Load(values.Cast<MessageGridRowDto>(), new DataSourceLoadOptionsBase { Select = select }).data;
    }
}
