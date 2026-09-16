using System.Collections;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ORP.Api.Infrastructure;

// Declares the Minimal API/OpenAPI query contract; DevExtreme parses the original query values.
public sealed record DevExtremeGridRequest(
    [property: FromQuery(Name = "skip")] int Skip,
    [property: FromQuery(Name = "take")] int Take,
    [property: FromQuery(Name = "sort")] string? Sort = null,
    [property: FromQuery(Name = "filter")] string? Filter = null,
    [property: FromQuery(Name = "group")] string? Group = null,
    [property: FromQuery(Name = "totalSummary")] string? TotalSummary = null,
    [property: FromQuery(Name = "groupSummary")] string? GroupSummary = null,
    [property: FromQuery(Name = "select")] string? Select = null,
    [property: FromQuery(Name = "requireTotalCount")] bool RequireTotalCount = false,
    [property: FromQuery(Name = "requireGroupCount")] bool RequireGroupCount = false,
    [property: FromQuery(Name = "isCountQuery")] bool IsCountQuery = false,
    [property: FromQuery(Name = "assignmentScope")] string? AssignmentScope = null);

public static class DevExtremeLoadOptions
{
    private const int MaxFilterDepth = 8;
    private const int MaxFilterConditions = 64;
    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "Id",
        ["externalId"] = "ExternalId",
        ["messageType"] = "MessageType",
        ["branchId"] = "BranchId",
        ["departmentId"] = "DepartmentId",
        ["state"] = "State",
        ["receivedAt"] = "ReceivedAt",
        ["currentAssigneeId"] = "CurrentAssigneeId",
        ["activeReviewId"] = "ActiveReviewId",
        ["activeReviewLevel"] = "ActiveReviewLevel",
        ["activeReviewerId"] = "ActiveReviewerId"
    };

    public static DataSourceLoadOptionsBase Parse(IQueryCollection query)
    {
        var options = new DataSourceLoadOptionsBase();
        try
        {
            DataSourceLoadOptionsParser.Parse(options, key => query[key].FirstOrDefault() ?? string.Empty);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or System.Text.Json.JsonException)
        {
            throw new FormatException("Invalid DevExtreme load options.", ex);
        }

        ValidateAndNormalize(options);
        options.PrimaryKey = ["Id"];
        options.SortByPrimaryKey = true;
        options.PaginateViaPrimaryKey = options.Group is not { Length: > 0 };
        options.RemoteSelect = true;
        options.RemoteGrouping = true;
        options.StringToLower = false;
        options.PreSelect = [.. Fields.Values, "RequiredReviewLevels", "UndoReviewId", "WorkflowDefinitionId", "CanChangeWorkflow", "CanReview"];
        if (options.Sort is not { Length: > 0 } && options.Group is not { Length: > 0 })
            options.Sort = [new SortingInfo { Selector = "ReceivedAt", Desc = true }];
        return options;
    }

    private static void ValidateAndNormalize(DataSourceLoadOptionsBase options)
    {
        if (options.Skip < 0) throw new FormatException("skip must be non-negative.");
        if (!options.IsCountQuery && options.Take is < 1 or > 500) throw new FormatException("take must be between 1 and 500.");
        if (options.Sort is { Length: > 5 }) throw new FormatException("At most 5 sort fields are supported.");
        if (options.Group is { Length: > 3 }) throw new FormatException("At most 3 group levels are supported.");
        if ((options.TotalSummary?.Length ?? 0) + (options.GroupSummary?.Length ?? 0) > 10)
            throw new FormatException("At most 10 summaries are supported.");

        foreach (var sort in options.Sort ?? []) sort.Selector = NormalizeField(sort.Selector);
        foreach (var group in options.Group ?? []) group.Selector = NormalizeField(group.Selector);
        foreach (var summary in (options.TotalSummary ?? []).Concat(options.GroupSummary ?? []))
            if (!string.IsNullOrWhiteSpace(summary.Selector)) summary.Selector = NormalizeField(summary.Selector);
        if (options.Select is not null)
            for (var i = 0; i < options.Select.Length; i++) options.Select[i] = NormalizeField(options.Select[i]);

        var conditions = 0;
        NormalizeFilter(options.Filter, 0, ref conditions);
    }

    private static void NormalizeFilter(IList? filter, int depth, ref int conditions)
    {
        if (filter is null) return;
        if (depth > MaxFilterDepth) throw new FormatException($"Filter depth cannot exceed {MaxFilterDepth}.");

        // DevExtreme owns the filter grammar and operators. Only enforce our field and size policy.
        if (filter.Count > 0 && filter[0] is string field && field != "!")
        {
            if (++conditions > MaxFilterConditions) throw new FormatException($"A filter cannot contain more than {MaxFilterConditions} conditions.");
            filter[0] = NormalizeField(field);
            return;
        }

        foreach (var item in filter)
            if (item is IList nested) NormalizeFilter(nested, depth + 1, ref conditions);
    }

    private static string NormalizeField(string? field)
    {
        if (field is null || !Fields.TryGetValue(field, out var normalized))
            throw new FormatException($"Unsupported grid field '{field}'.");
        return normalized;
    }
}
