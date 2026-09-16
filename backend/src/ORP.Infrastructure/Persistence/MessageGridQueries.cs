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
    public required long Id { get; init; }
    public required string ExternalId { get; init; }
    public required string MessageType { get; init; }
    public required int BranchId { get; init; }
    public required int DepartmentId { get; init; }
    public required MessageState State { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public required int? CurrentAssigneeId { get; init; }
    public required long? ActiveReviewId { get; init; }
    public required int? ActiveReviewLevel { get; init; }
    public required int? ActiveReviewerId { get; init; }
    public required long? UndoReviewId { get; init; }
    public required int WorkflowDefinitionId { get; init; }
    public required bool CanReview { get; init; }
    public required bool CanChangeWorkflow { get; init; }
    public required int[] RequiredReviewLevels { get; init; }
}

public sealed class MessageGridQueries(ORPDbContext db)
{
    public async Task<IReadOnlyList<MessageStateCountDto>> StateCountsAsync(UserAccess access, CancellationToken ct)
    {
        var counts = await db.ReadAccessibleMessages(access.UserId).GroupBy(x => x.State)
            .Select(g => new { State = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.State, x => x.Count, ct);
        return Enum.GetValues<MessageState>().Select(state => new MessageStateCountDto(state, counts.GetValueOrDefault(state))).ToArray();
    }

    public async Task<LoadResult> LoadAsync(DataSourceLoadOptionsBase options, UserAccess access,
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
        try
        {
            return await DataSourceLoader.LoadAsync(rows, options, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IndexOutOfRangeException)
        {
            throw new FormatException("Invalid DevExtreme load options.", ex);
        }
    }
}

public static class MessageAssignmentScopes
{
    public const string Mine = "mine";
    public const string Departments = "departments";
    public const string Assignable = "assignable";
}
