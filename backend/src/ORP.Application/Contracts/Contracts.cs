using System.ComponentModel.DataAnnotations;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;

namespace ORP.Application.Abstractions;

public sealed record AssignMessageRequest([property: Range(1, int.MaxValue)] int AssignedTo);
public sealed record StartReviewRequest([property: Range(1, 3)] int Level);
public sealed record ApproveReviewRequest([property: Range(1, 3)] int Level,
    [property: StringLength(2000)] string? Comment);
public sealed record RejectReviewRequest([property: Range(1, 3)] int Level,
    [property: StringLength(2000)] string? Comment);
public sealed record UndoReviewRequest([property: Range(1, long.MaxValue)] long ReviewId);

public sealed record StartReviewResponse(long ReviewId);
public sealed record CurrentUserResponse(int UserId, string UserName, IReadOnlyList<string> Permissions,
    IReadOnlyList<int> Branches, IReadOnlyList<int> Departments);

public sealed record MessageDetailsDto(long Id, string ExternalId, string MessageType, int BranchId, int DepartmentId,
    MessageState State, DateTimeOffset ReceivedAt, int? CurrentAssigneeId, string Sender, string Receiver,
    string? Account, string? Currency, decimal? Amount, string? Reference, string? Body);
public sealed record MessageListItemDto(long Id, string ExternalId, string MessageType, int BranchId, int DepartmentId,
    MessageState State, DateTimeOffset ReceivedAt, int? CurrentAssigneeId, string? Account, string? Currency, decimal? Amount);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
public sealed record AuditTrailRequest(int Skip = 0, int Take = 100);
public sealed record SortClause([property: Required] string Field,
    [property: Required, RegularExpression("^(?i:asc|desc)$")] string Direction);
public sealed record MessageFilter(IReadOnlyList<MessageState>? States, IReadOnlyList<int>? Branches,
    IReadOnlyList<string>? MessageTypes, IReadOnlyList<int>? Departments, DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo, string? Account, string? Currency);
public sealed record MessageSearchRequest([property: Range(0, int.MaxValue)] int Skip,
    [property: Range(1, 500)] int Take, IReadOnlyList<SortClause>? Sort, MessageFilter? Filter);
public sealed record DashboardSummaryDto(int Total, int Pending, int WaitingForFirstReview,
    int WaitingForSecondReview, int WaitingForThirdReview, int Completed);
public sealed record AuditActorDto(int UserId, string UserName, string DisplayName);
public sealed record AuditEventDetailsDto(int? WorkflowDefinitionId = null, int? PreviousAssigneeId = null,
    int? AssigneeId = null, long? ReviewId = null, int? ReviewLevel = null, string? Comment = null);
public sealed record AuditEventDto(long Id, AuditEventType EventType, DateTimeOffset Timestamp,
    string? OldState, string? NewState, AuditActorDto? Actor,
    AuditEventDetailsDto Details, string CorrelationId);
public sealed record WorkflowStepDto(int Order, int ReviewLevel, bool Required);
public sealed record WorkflowSummaryDto(int Id, string Name, string MessageType, int DepartmentId, int? BranchId,
    bool IsActive, IReadOnlyList<WorkflowStepDto> Steps);
public sealed record UserSummaryDto(int Id, string UserName, string DisplayName, IReadOnlyList<int> BranchIds,
    IReadOnlyList<int> DepartmentIds);
public sealed record ReferenceItemDto(int Id, string Name);
public sealed record MessageStateReferenceDto(string Code, string Label);
