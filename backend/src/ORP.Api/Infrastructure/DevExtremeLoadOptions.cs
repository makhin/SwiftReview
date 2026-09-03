using System.Collections;
using System.Globalization;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ORP.Api.Infrastructure;

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
    [property: FromQuery(Name = "isCountQuery")] bool IsCountQuery = false);

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
        ["account"] = "Account",
        ["currency"] = "Currency",
        ["amount"] = "Amount"
    };
    private static readonly HashSet<string> StringFields = new(StringComparer.OrdinalIgnoreCase)
    { "ExternalId", "MessageType", "Account", "Currency" };
    private static readonly HashSet<string> StringOperations = new(StringComparer.OrdinalIgnoreCase)
    { "startswith", "endswith", "contains", "notcontains" };
    private static readonly HashSet<string> FilterOperations = new(StringComparer.OrdinalIgnoreCase)
    { "=", "<>", ">", ">=", "<", "<=", "startswith", "endswith", "contains", "notcontains" };
    private static readonly HashSet<string> SummaryTypes = new(StringComparer.OrdinalIgnoreCase)
    { "count", "sum", "avg", "min", "max" };

    public static DataSourceLoadOptionsBase Parse(IQueryCollection query)
        => Parse(key => query[key].FirstOrDefault() ?? string.Empty);

    public static DataSourceLoadOptionsBase Parse(DevExtremeGridRequest request)
        => Parse(key => key switch
        {
            "skip" => request.Skip.ToString(CultureInfo.InvariantCulture),
            "take" => request.Take.ToString(CultureInfo.InvariantCulture),
            "sort" => request.Sort ?? string.Empty,
            "filter" => request.Filter ?? string.Empty,
            "group" => request.Group ?? string.Empty,
            "totalSummary" => request.TotalSummary ?? string.Empty,
            "groupSummary" => request.GroupSummary ?? string.Empty,
            "select" => request.Select ?? string.Empty,
            "requireTotalCount" => request.RequireTotalCount.ToString(CultureInfo.InvariantCulture),
            "requireGroupCount" => request.RequireGroupCount.ToString(CultureInfo.InvariantCulture),
            "isCountQuery" => request.IsCountQuery.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        });

    private static DataSourceLoadOptionsBase Parse(Func<string, string> valueSource)
    {
        var options = new DataSourceLoadOptionsBase();
        try
        {
            DataSourceLoadOptionsParser.Parse(options, valueSource);
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
        options.PreSelect = Fields.Values.ToArray();
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
        {
            if (!SummaryTypes.Contains(summary.SummaryType ?? string.Empty)) throw new FormatException("Unsupported summary type.");
            if (!string.IsNullOrWhiteSpace(summary.Selector)) summary.Selector = NormalizeField(summary.Selector);
            else if (!string.Equals(summary.SummaryType, "count", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("A summary selector is required.");
            if (summary.SummaryType is not null &&
                (summary.SummaryType.Equals("sum", StringComparison.OrdinalIgnoreCase) || summary.SummaryType.Equals("avg", StringComparison.OrdinalIgnoreCase)) &&
                summary.Selector != "Amount")
                throw new FormatException("sum and avg summaries are supported only for amount.");
        }
        if (options.Select is not null)
            for (var i = 0; i < options.Select.Length; i++) options.Select[i] = NormalizeField(options.Select[i]);

        var conditions = 0;
        NormalizeFilter(options.Filter, 0, ref conditions);
    }

    private static void NormalizeFilter(IList? filter, int depth, ref int conditions)
    {
        if (filter is null) return;
        if (depth > MaxFilterDepth) throw new FormatException($"Filter depth cannot exceed {MaxFilterDepth}.");

        if (filter.Count == 2 && filter[0] is string unary && unary == "!" && filter[1] is IList operand)
        {
            NormalizeFilter(operand, depth + 1, ref conditions);
            return;
        }

        if (filter.Count == 3 && filter[0] is string field && filter[1] is string operation)
        {
            if (++conditions > MaxFilterConditions) throw new FormatException($"A filter cannot contain more than {MaxFilterConditions} conditions.");
            if (!FilterOperations.Contains(operation)) throw new FormatException("Unsupported filter operation.");
            var normalized = NormalizeField(field);
            if (StringOperations.Contains(operation) && !StringFields.Contains(normalized))
                throw new FormatException($"The {operation} operation is supported only for string fields.");
            filter[0] = normalized;
            return;
        }

        if (filter.Count < 3 || filter.Count % 2 == 0) throw new FormatException("Malformed filter expression.");
        for (var i = 0; i < filter.Count; i++)
        {
            if (i % 2 == 0)
            {
                if (filter[i] is not IList nested) throw new FormatException("Malformed filter expression.");
                NormalizeFilter(nested, depth + 1, ref conditions);
            }
            else if (filter[i] is not string connector ||
                !(connector.Equals("and", StringComparison.OrdinalIgnoreCase) || connector.Equals("or", StringComparison.OrdinalIgnoreCase)))
                throw new FormatException("Malformed filter expression.");
        }
    }

    private static string NormalizeField(string? field)
    {
        if (field is null || !Fields.TryGetValue(field, out var normalized))
            throw new FormatException($"Unsupported grid field '{field}'.");
        return normalized;
    }
}
