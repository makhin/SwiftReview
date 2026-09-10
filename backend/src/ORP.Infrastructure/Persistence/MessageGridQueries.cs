using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;

namespace ORP.Infrastructure.Persistence;

public sealed class MessageGridRowDto
{
    public long Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public string MessageType { get; init; } = null!;
    public int BranchId { get; init; }
    public int DepartmentId { get; init; }
    public MessageState State { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public int? CurrentAssigneeId { get; init; }
    public long? ActiveReviewId { get; init; }
    public int? ActiveReviewLevel { get; init; }
    public int? ActiveReviewerId { get; init; }
    public long? UndoReviewId { get; init; }
    public int WorkflowDefinitionId { get; init; }
    public bool CanChangeWorkflow { get; init; }
    public int[] RequiredReviewLevels { get; init; } = [];
}

public sealed class MessageGridQueries(ORPDbContext db)
{
    public Task<LoadResult> LoadAsync(DataSourceLoadOptionsBase options, UserAccess access,
        string? assignmentScope, CancellationToken ct)
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
        var rows = query
            .Select(x => new MessageGridRowDto
            {
                Id = x.Id,
                ExternalId = x.ExternalId,
                MessageType = x.MessageType,
                BranchId = x.BranchId,
                DepartmentId = x.DepartmentId,
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
                        role.BranchId == x.BranchId && role.DepartmentId == x.DepartmentId &&
                        role.Role.Permissions.Any(grant => grant.Permission.Name == Permissions.WorkflowManage))),
                UndoReviewId = access.IsGlobalAdministrator &&
                    (x.State == MessageState.WaitingForSecondReview || x.State == MessageState.WaitingForThirdReview || x.State == MessageState.Completed)
                    ? db.Reviews.Where(review => review.MessageId == x.Id && review.Status == ReviewStatus.Approved &&
                        db.WorkflowSteps.Any(step => step.WorkflowDefinitionId == x.WorkflowDefinitionId && step.Required && step.ReviewLevel == review.Level))
                        .OrderByDescending(review => review.Level).Select(review => (long?)review.Id).FirstOrDefault()
                    : null,
                RequiredReviewLevels = db.WorkflowSteps
                    .Where(step => step.WorkflowDefinitionId == x.WorkflowDefinitionId && step.Required)
                    .OrderBy(step => step.Order)
                    .Select(step => step.ReviewLevel).ToArray()
            });
        return DataSourceLoader.LoadAsync(rows, options, ct);
    }
}

public static class MessageAssignmentScopes
{
    public const string Mine = "mine";
    public const string Departments = "departments";
    public const string Assignable = "assignable";
}
