using System.Globalization;
using System.Text.RegularExpressions;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using ORP.Domain.Messages;

namespace ORP.Infrastructure.Persistence;

internal sealed class MessageGridOptions(GridQuery<MessageReadRow> paging, MessageState? status,
    string? messageType, MessageDirection? direction, int? branch, DateTimeOffset? from, DateTimeOffset? to,
    bool exclusiveEnd, string? search)
{
    public static MessageGridOptions Create(MessageGridRequest request)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 500 || (long)(request.Page - 1) * request.PageSize > int.MaxValue)
            throw new FormatException("page must be positive, pageSize must be between 1 and 500, and the page offset must fit Int32.");
        if (request.AssignmentScope is not (null or MessageAssignmentScopes.Mine))
            throw new FormatException("Unsupported message assignment scope.");
        if (request.Branch is < 1) throw new FormatException("branch must be a positive integer branch ID.");
        var from = ParseDate(request.DateFrom, false);
        var to = ParseDate(request.DateTo, true);
        var exclusiveEnd = !string.IsNullOrWhiteSpace(request.DateTo) && IsDateOnly(request.DateTo);
        if (from is not null && to is not null && (from > to || (from == to && exclusiveEnd)))
            throw new FormatException("dateFrom must not be after dateTo.");
        var search = Clean(request.Search);
        if (search?.Length > 100) throw new FormatException("search cannot exceed 100 characters.");
        var paging = GridQuery<MessageReadRow>.Create(
            new GridRequest((request.Page - 1) * request.PageSize, request.PageSize, request.Sort),
            GridFields.Messages, 500, 5, new SortClause("receivedAt", "desc"));
        return new(paging, ParseEnum<MessageState>(request.Status), Clean(request.MessageType),
            ParseEnum<MessageDirection>(request.Direction), request.Branch, from, to, exclusiveEnd, search);
    }

    public IQueryable<MessageReadRow> Filter(IQueryable<MessageReadRow> query)
    {
        if (status is { } state) query = query.Where(x => x.State == state);
        if (messageType is not null) query = query.Where(x => x.MessageType == messageType);
        if (direction is { } value) query = query.Where(x => x.Direction == value);
        if (branch is { } branchId) query = query.Where(x => x.BranchId == branchId);
        if (from is { } start) query = query.Where(x => x.ReceivedAt >= start);
        if (to is { } end) query = exclusiveEnd
            ? query.Where(x => x.ReceivedAt < end) : query.Where(x => x.ReceivedAt <= end);
        if (search is not null) query = query.Where(x => x.ExternalId.Contains(search) || x.MessageType.Contains(search));
        return query;
    }

    public IQueryable<MessageReadRow> Page(IQueryable<MessageReadRow> query) => paging.Page(query);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static T? ParseEnum<T>(string? value) where T : struct, Enum
    {
        if (Clean(value) is not { } text) return null;
        var name = Enum.GetNames<T>().FirstOrDefault(name => name.Equals(text, StringComparison.OrdinalIgnoreCase));
        return name is not null ? Enum.Parse<T>(name) : throw new FormatException($"Invalid filter value for {typeof(T).Name} field.");
    }

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
