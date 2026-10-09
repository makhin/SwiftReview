using System.Text.Json;
using System.Text.Json.Serialization;
using ORP.Application.Abstractions;
using ORP.Domain.Messages;

namespace ORP.Application.Grids;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public record GridRequest(int Skip = 0, int Take = 20,
    IReadOnlyList<SortClause>? Sort = null, GridFilter? Filter = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record MessageGridRequest(string? Search = null, string? Status = null,
    string? MessageType = null, string? Branch = null, string? DateFrom = null, string? DateTo = null,
    int Page = 1, int PageSize = 20, IReadOnlyList<SortClause>? Sort = null, string? AssignmentScope = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record AdminUserGridRequest(int Skip = 0, int Take = 20,
    IReadOnlyList<SortClause>? Sort = null, GridFilter? Filter = null, string? Search = null)
    : GridRequest(Skip, Take, Sort, Filter);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed record GridFilter
{
    public string? Field { get; init; }
    public string? Operator { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Value { get; init; }
    public string? Logic { get; init; }
    public IReadOnlyList<GridFilter?>? Filters { get; init; }
}

public static class GridFields
{
    public static IReadOnlyList<string> Messages { get; } = Array.AsReadOnly(new[]
    {
        "Id", "ExternalId", "Direction", "MessageType", "BranchId", "DepartmentId", "State", "ReceivedAt",
        "CurrentAssigneeId", "ActiveReviewId", "ActiveReviewLevel", "ActiveReviewerId", "WorkflowDefinitionId"
    });
    public static IReadOnlyList<string> Users { get; } = Array.AsReadOnly(new[] { "Id", "UserName", "DisplayName" });
}

public sealed class MessageGridRowDto
{
    public required long Id { get; init; }
    public required string ExternalId { get; init; }
    public required MessageDirection? Direction { get; init; }
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

public sealed record AdminUserGridRow
{
    public int Id { get; init; }
    public string UserName { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
}

public static class MessageAssignmentScopes
{
    public const string Mine = "mine";
    public const string Departments = "departments";
    public const string Assignable = "assignable";
}
