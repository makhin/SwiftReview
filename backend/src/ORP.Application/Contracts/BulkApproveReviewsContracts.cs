using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ORP.Application.Abstractions;

public sealed record BulkApproveReviewsRequest(
    [property: Required, MinLength(1), MaxLength(100)] IReadOnlyList<long> MessageIds,
    [property: StringLength(2000)] string? Comment = null);

public sealed record BulkApproveReviewsResponse(IReadOnlyList<BulkApproveMessageResult> Results);

public sealed record BulkApproveMessageResult(long MessageId, int Status)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? ApprovedLevels { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BulkApproveError? Error { get; init; }
}

public sealed record BulkApproveError(string Code, string Detail);
