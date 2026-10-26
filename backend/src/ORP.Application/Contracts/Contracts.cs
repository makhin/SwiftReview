using System.ComponentModel.DataAnnotations;
using ORP.Application.ReferenceData;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;

namespace ORP.Application.Abstractions;

public sealed record AssignMessageRequest([property: Range(1, int.MaxValue)] int AssignedTo, [property: Range(1, 3)] int? Level = null);
public sealed record ChangeMessageWorkflowRequest([property: Range(1, int.MaxValue)] int WorkflowDefinitionId);
public sealed record ConfirmReviewRequest([property: Range(1, 3)] int Level,
    [property: StringLength(2000)] string? Comment = null);
public sealed record StartReviewRequest([property: Range(1, 3)] int Level);
public sealed record CancelReviewRequest([property: Range(1, 3)] int Level, [property: Range(1, long.MaxValue)] long ReviewId);
public sealed record ApproveReviewRequest([property: Range(1, 3)] int Level, [property: Range(1, long.MaxValue)] long ReviewId,
    [property: StringLength(2000)] string? Comment);
public sealed record RejectReviewRequest([property: Range(1, 3)] int Level, [property: Range(1, long.MaxValue)] long ReviewId,
    [property: StringLength(2000)] string? Comment);
public sealed record UndoReviewRequest([property: Range(1, long.MaxValue)] long ReviewId,
    [property: StringLength(2000)] string? Comment = null);

public sealed record StartReviewResponse(long ReviewId);
public sealed record CurrentUserResponse(int UserId, string UserName, string DisplayName, IReadOnlyList<string> Permissions,
    IReadOnlyList<int> Branches, IReadOnlyList<int> Departments, bool IsGlobalAdministrator,
    IReadOnlyList<UserScopeAccess> Scopes)
{
    public string RolesAndDepartments { get; init; } = string.Empty;
}

public sealed record LevelAssignmentDto(int Level, int AssignedTo, string AssigneeName);

public sealed record MessageDetailsDto(long Id, string ExternalId, string MessageType, int BranchId, IReadOnlyList<int> DepartmentIds,
    MessageState State, DateTimeOffset ReceivedAt, int? CurrentAssigneeId, string Sender, string Receiver,
    string? Body, MessageDirection? Direction)
{
    public string? TransactionReferenceNumber { get; init; }
    public string? RelatedReference { get; init; }
    public IReadOnlyList<LevelAssignmentDto> Assignments { get; init; } = [];
    public string Version { get; init; } = string.Empty;
    public ConfirmationMode ConfirmationMode { get; init; }
    public bool CanConfirm { get; init; }
    public int? ConfirmationLevel { get; init; }
    public bool CanReview { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string DepartmentNames { get; init; } = string.Empty;
    public string? CurrentAssigneeName { get; init; }
    public string StateName => MessageStates.GetName(State);
}
public sealed record MessageListItemDto(long Id, string ExternalId, string MessageType, int BranchId, IReadOnlyList<int> DepartmentIds,
    MessageState State, DateTimeOffset ReceivedAt, int? CurrentAssigneeId, long? ActiveReviewId,
    int? ActiveReviewLevel, int? ActiveReviewerId, MessageDirection? Direction)
{
    public string? TransactionReferenceNumber { get; init; }
    public string? RelatedReference { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string DepartmentNames { get; init; } = string.Empty;
    public string? CurrentAssigneeName { get; init; }
    public string? ActiveReviewerName { get; init; }
    public string StateName => MessageStates.GetName(State);
}
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
public sealed record AuditTrailRequest(int Skip = 0, int Take = 100);
public sealed record SortClause([property: Required] string Field,
    [property: Required, RegularExpression("^(?i:asc|desc)$")] string Direction);
public sealed record MessageFilter(IReadOnlyList<MessageState>? States, IReadOnlyList<int>? Branches,
    IReadOnlyList<string>? MessageTypes, IReadOnlyList<int>? Departments, DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo);
public sealed record MessageSearchRequest([property: Range(0, int.MaxValue)] int Skip,
    [property: Range(1, 500)] int Take, IReadOnlyList<SortClause>? Sort, MessageFilter? Filter);
public sealed record DashboardSummaryDto(int TotalMessages, int ViewOnly, int Unassigned, int PendingFirstConfirmation,
    int PendingSecondConfirmation, int Completed)
{
    public static DashboardSummaryDto FromStateCounts(IReadOnlyDictionary<MessageState, int> counts) => new(
        counts.Values.Sum(),
        counts.GetValueOrDefault(MessageState.ViewOnly),
        counts.GetValueOrDefault(MessageState.New),
        counts.GetValueOrDefault(MessageState.Assigned) + counts.GetValueOrDefault(MessageState.FirstReviewInProgress),
        counts.GetValueOrDefault(MessageState.WaitingForSecondReview) + counts.GetValueOrDefault(MessageState.SecondReviewInProgress),
        counts.GetValueOrDefault(MessageState.Completed));
}
public sealed record AuditActorDto(int UserId, string UserName, string DisplayName);
public sealed record AuditEventDetailsDto(int? WorkflowDefinitionId = null, int? PreviousAssigneeId = null,
    int? AssigneeId = null, long? ReviewId = null, int? ReviewLevel = null, string? Comment = null,
    int? PreviousBranchId = null, int? BranchId = null, IReadOnlyList<int>? PreviousDepartmentIds = null,
    IReadOnlyList<int>? DepartmentIds = null, int? PreviousWorkflowDefinitionId = null);
public sealed record AuditEventDto(long Id, AuditEventType EventType, DateTimeOffset Timestamp,
    string? OldState, string? NewState, AuditActorDto? Actor,
    AuditEventDetailsDto Details, string CorrelationId);
public sealed record WorkflowStepDto(int Order, int ReviewLevel, bool Required);
public sealed record WorkflowSummaryDto(int Id, string Code, string Name, bool IsActive,
    IReadOnlyList<WorkflowStepDto> Steps)
{
    public UndoApprovalMode UndoApprovalMode { get; init; }
    public UndoActorMode UndoActorMode { get; init; }
    public UndoActiveReviewMode UndoActiveReviewMode { get; init; }
    public ConfirmationMode ConfirmationMode { get; init; }
    public bool AllowReviewerReuse { get; init; }
    public AssignmentRetentionMode AssignmentRetentionMode { get; init; }
    public bool RejectAllowed { get; init; }
}
public sealed record UserSummaryDto(int Id, string UserName, string DisplayName, IReadOnlyList<int> BranchIds,
    IReadOnlyList<int> DepartmentIds);
public sealed record AssignmentCandidateDto(int Id, string UserName, string DisplayName);
public sealed record ReferenceItemDto(int Id, string Name);
public sealed record MessageStateCountsDto(IReadOnlyList<MessageStateCountDto> Items, MessageCountDto Total);
public record MessageCountDto(int Count, IReadOnlyList<MessageTypeCountDto> Breakdown);
public sealed record MessageTypeCountDto(string MessageType, int Count);
public sealed record MessageStateCountDto(MessageState State, int Count, IReadOnlyList<MessageTypeCountDto> Breakdown)
    : MessageCountDto(Count, Breakdown)
{
    public string StateName => MessageStates.GetName(State);
}
public enum MessageStagePhase { Waiting, Assigned, Reviewing, Completed, Rejected, ViewOnly }

// AssignedDescription overrides Description when the message has an assignee.
public sealed record MessageStateReferenceDto(string Code, string Label, string Description,
    int? ReviewLevel, MessageStagePhase Phase, string? AssignedDescription);
