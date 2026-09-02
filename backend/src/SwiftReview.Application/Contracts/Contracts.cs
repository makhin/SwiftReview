using System.ComponentModel.DataAnnotations;
using SwiftReview.Domain.Messages;

namespace SwiftReview.Application.Abstractions;

public sealed record ImportMessageRequest(
    [property: Required, StringLength(100)] string ExternalId,
    [property: Required, StringLength(20)] string MessageType,
    [property: Range(1, int.MaxValue)] int BranchId,
    [property: Range(1, int.MaxValue)] int DepartmentId,
    DateTimeOffset ReceivedAt,
    [property: Required, StringLength(100)] string Sender,
    [property: Required, StringLength(100)] string Receiver,
    [property: StringLength(100)] string? Account,
    [property: StringLength(3, MinimumLength = 3)] string? Currency,
    decimal? Amount,
    [property: StringLength(200)] string? Reference,
    [property: Required] string RawContent);
public sealed record AssignMessageRequest([property: Range(1, int.MaxValue)] int AssignedTo, [property: Required] string RowVersion);
public sealed record StartReviewRequest([property: Range(1, 3)] int Level, [property: Required] string RowVersion);
public sealed record ApproveReviewRequest([property: Range(1, 3)] int Level, [property: Required] string RowVersion,
    [property: StringLength(2000)] string? Comment);
public sealed record RejectReviewRequest([property: Range(1, 3)] int Level, [property: Required] string RowVersion,
    [property: Required, StringLength(2000)] string Comment);
public sealed record UndoReviewRequest([property: Range(1, long.MaxValue)] long ReviewId, [property: Required] string RowVersion);

public sealed record ImportMessageResponse(long Id, bool Duplicate);
public sealed record StartReviewResponse(long ReviewId);
public sealed record CurrentUserResponse(int UserId, string UserName, IReadOnlyList<string> Permissions,
    IReadOnlyList<int> Branches, IReadOnlyList<int> Departments);

public sealed record MessageDetailsDto(long Id, string ExternalId, string MessageType, int BranchId, int DepartmentId,
    MessageState State, DateTimeOffset ReceivedAt, int? CurrentAssigneeId, string Sender, string Receiver,
    string? Account, string? Currency, decimal? Amount, string? Reference, string RowVersion);
public sealed record MessageListItemDto(long Id, string ExternalId, string MessageType, int BranchId, int DepartmentId,
    MessageState State, DateTimeOffset ReceivedAt, int? CurrentAssigneeId, string? Account, string? Currency, decimal? Amount, string RowVersion);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
public sealed record SortClause([property: Required] string Field,
    [property: Required, RegularExpression("^(?i:asc|desc)$")] string Direction);
public sealed record MessageFilter(IReadOnlyList<MessageState>? States, IReadOnlyList<int>? Branches,
    IReadOnlyList<string>? MessageTypes, IReadOnlyList<int>? Departments, DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo, string? Account, string? Currency);
public sealed record MessageSearchRequest([property: Range(0, int.MaxValue)] int Skip,
    [property: Range(1, 500)] int Take, IReadOnlyList<SortClause>? Sort, MessageFilter? Filter);
public sealed record DashboardSummaryDto(int Total, int Pending, int WaitingForFirstReview,
    int WaitingForSecondReview, int WaitingForThirdReview, int Completed);
public sealed record AuditEventDto(long Id, string EventType, int? UserId, DateTimeOffset Timestamp,
    string? OldState, string? NewState, string DetailsJson, string CorrelationId);
public sealed record WorkflowStepDto(int Order, int ReviewLevel, bool Required);
public sealed record WorkflowSummaryDto(int Id, string Name, string MessageType, int DepartmentId, int? BranchId,
    bool IsActive, IReadOnlyList<WorkflowStepDto> Steps);
public sealed record UserSummaryDto(int Id, string UserName, string DisplayName, IReadOnlyList<int> BranchIds,
    IReadOnlyList<int> DepartmentIds);
