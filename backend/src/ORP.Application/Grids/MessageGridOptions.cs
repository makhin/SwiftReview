using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ORP.Application.Grids;

public static class MessageGridOptions
{
    public static GridRequest Create(MessageGridRequest request)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 500 || (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            throw new FormatException("page must be positive, pageSize must be between 1 and 500, and the page offset must fit Int32.");
        if (request.AssignmentScope is not (null or MessageAssignmentScopes.Mine or MessageAssignmentScopes.Departments or MessageAssignmentScopes.Assignable))
            throw new FormatException("Unsupported message assignment scope.");
        var filters = new List<GridFilter>();
        void Add(string field, string op, object value) => filters.Add(Condition(field, op, value));
        if (!string.IsNullOrWhiteSpace(request.Status)) Add("state", "eq", request.Status.Trim());
        if (!string.IsNullOrWhiteSpace(request.MessageType)) Add("messageType", "eq", request.MessageType.Trim());
        if (!string.IsNullOrWhiteSpace(request.Branch))
        {
            if (!int.TryParse(request.Branch.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var branch) || branch < 1)
                throw new FormatException("branch must be a positive numeric branch ID encoded as a string.");
            Add("branchId", "eq", branch);
        }
        var from = ParseDate(request.DateFrom, false);
        var to = ParseDate(request.DateTo, true);
        if (from is not null && to is not null && (from > to || (from == to && IsDateOnly(request.DateTo!))))
            throw new FormatException("dateFrom must not be after dateTo.");
        if (from is not null) Add("receivedAt", "gte", from.Value);
        if (to is not null) Add("receivedAt", IsDateOnly(request.DateTo!) ? "lt" : "lte", to.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            if (search.Length > 100) throw new FormatException("search cannot exceed 100 characters.");
            filters.Add(new GridFilter { Logic = "or", Filters = [Condition("externalId", "contains", search), Condition("messageType", "contains", search)] });
        }
        return new GridRequest((request.Page - 1) * request.PageSize, request.PageSize, request.Sort,
            filters.Count == 0 ? null : new GridFilter { Logic = "and", Filters = filters });
    }

    private static GridFilter Condition(string field, string op, object value) => new() { Field = field, Operator = op, Value = JsonSerializer.SerializeToElement(value) };
    private static bool IsDateOnly(string value) => Regex.IsMatch(value.Trim(), @"^\d{4}-\d{2}-\d{2}$");
    private static DateTimeOffset? ParseDate(string? value, bool end)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (IsDateOnly(value) && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            if (end && day == DateOnly.MaxValue) throw new FormatException("dateTo is outside the supported range.");
            return new DateTimeOffset((end ? day.AddDays(1) : day).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }
        if (Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$") && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        throw new FormatException("Dates must be yyyy-MM-dd (UTC) or ISO 8601 timestamps with seconds and a timezone.");
    }
}
