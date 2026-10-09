using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ORP.Application.Abstractions;

namespace ORP.Application.Grids;

// Builds provider-translatable expressions; field names can only resolve through the supplied allowlist.
public sealed class GridQuery<T>
{
    private readonly GridRequest request;
    private readonly Expression<Func<T, bool>>? predicate;
    private readonly IReadOnlyList<(LambdaExpression Key, bool Descending)> sorting;

    private GridQuery(GridRequest request, Expression<Func<T, bool>>? predicate,
        IReadOnlyList<(LambdaExpression Key, bool Descending)> sorting)
    {
        this.request = request;
        this.predicate = predicate;
        this.sorting = sorting;
    }

    public static GridQuery<T> Create(GridRequest request, IReadOnlyList<string> fields,
        int maxTake, int maxSort, SortClause defaultSort)
    {
        if (request.Skip < 0 || request.Take < 1 || request.Take > maxTake)
            throw new FormatException($"skip must be non-negative and take must be between 1 and {maxTake}.");
        if (request is AdminUserGridRequest { Search.Length: > 100 })
            throw new FormatException("User search cannot exceed 100 characters.");
        if (request.Sort?.Count > maxSort)
            throw new FormatException($"At most {maxSort} sort fields are supported.");
        var properties = fields.ToDictionary(field => field,
            field => typeof(T).GetProperty(field)!, StringComparer.OrdinalIgnoreCase);
        var parameter = Expression.Parameter(typeof(T), "row");
        var conditions = 0;
        var body = request.Filter is null ? null : BuildFilter(request.Filter, parameter, properties, 0, ref conditions);
        var sorts = new List<(LambdaExpression Key, bool Descending)>();
        var clauses = request.Sort is { Count: > 0 } ? request.Sort : [defaultSort];
        foreach (var clause in clauses)
        {
            if (clause is null || clause.Direction is null ||
                !(clause.Direction.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                  clause.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase)))
                throw new FormatException("Sort direction must be asc or desc.");
            var member = Expression.Property(parameter, FindProperty(properties, clause.Field));
            sorts.Add((Expression.Lambda(member, parameter), clause.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase)));
        }
        if (!clauses.Any(clause => string.Equals(clause.Field, "id", StringComparison.OrdinalIgnoreCase)))
            sorts.Add((Expression.Lambda(Expression.Property(parameter, FindProperty(properties, "id")), parameter), false));
        return new GridQuery<T>(request, body is null ? null : Expression.Lambda<Func<T, bool>>(body, parameter), sorts);
    }

    public IQueryable<T> Filter(IQueryable<T> query) => predicate is null ? query : query.Where(predicate);

    public IQueryable<T> Page(IQueryable<T> query)
    {
        for (var i = 0; i < sorting.Count; i++)
        {
            var (key, descending) = sorting[i];
            var method = i == 0
                ? descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy)
                : descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
            query = query.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method,
                [typeof(T), key.ReturnType], query.Expression, Expression.Quote(key)));
        }
        return query.Skip(request.Skip).Take(request.Take);
    }

    private static PropertyInfo FindProperty(Dictionary<string, PropertyInfo> properties, string? field) =>
        field is not null && properties.TryGetValue(field, out var property)
            ? property : throw new FormatException($"Unsupported grid field '{field}'.");

    private static Expression BuildFilter(GridFilter filter, ParameterExpression parameter,
        Dictionary<string, PropertyInfo> properties, int depth, ref int conditions)
    {
        if (depth > 8) throw new FormatException("Filter depth cannot exceed 8.");
        if (filter.Logic is not null || filter.Filters is not null)
        {
            if (filter.Field is not null || filter.Operator is not null || filter.Value.ValueKind != JsonValueKind.Undefined ||
                filter.Filters is not { Count: > 0 } ||
                !(string.Equals(filter.Logic, "and", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(filter.Logic, "or", StringComparison.OrdinalIgnoreCase)))
                throw new FormatException("A filter group must contain and/or logic and a non-empty filters array only.");
            Expression? group = null;
            foreach (var child in filter.Filters)
            {
                if (child is null) throw new FormatException("A filter group cannot contain null conditions.");
                var expression = BuildFilter(child, parameter, properties, depth + 1, ref conditions);
                group = group is null ? expression : string.Equals(filter.Logic, "and", StringComparison.OrdinalIgnoreCase)
                    ? Expression.AndAlso(group, expression) : Expression.OrElse(group, expression);
            }
            return group!;
        }
        if (++conditions > 64) throw new FormatException("A filter cannot contain more than 64 conditions.");
        if (filter.Operator is null || filter.Value.ValueKind == JsonValueKind.Undefined)
            throw new FormatException("A filter condition requires field, operator and value.");
        var member = Expression.Property(parameter, FindProperty(properties, filter.Field));
        var type = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        var op = filter.Operator.ToLowerInvariant();
        if (op is not ("eq" or "ne" or "gt" or "gte" or "lt" or "lte" or "contains" or "startswith" or "endswith"))
            throw new FormatException("Unsupported filter operator.");
        var value = ReadValue(filter.Value, member.Type);
        if (value is null && op is not ("eq" or "ne"))
            throw new FormatException("null is only supported with eq/ne.");
        Expression constant = value is null ? Expression.Constant(null, member.Type)
            : CaptureValue(value, member.Type);
        if (op is "eq" or "ne")
            return op == "eq" ? Expression.Equal(member, constant) : Expression.NotEqual(member, constant);
        if (op is "contains" or "startswith" or "endswith")
        {
            if (type != typeof(string)) throw new FormatException("Text operators require a string field.");
            var method = op switch { "contains" => nameof(string.Contains), "startswith" => nameof(string.StartsWith), _ => nameof(string.EndsWith) };
            return Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, member.Type)),
                Expression.Call(member, method, Type.EmptyTypes, constant));
        }
        if (type != typeof(int) && type != typeof(long) && type != typeof(DateTimeOffset))
            throw new FormatException("Ordered comparisons require a numeric or date field.");
        return op switch
        {
            "gt" => Expression.GreaterThan(member, constant),
            "gte" => Expression.GreaterThanOrEqual(member, constant),
            "lt" => Expression.LessThan(member, constant),
            _ => Expression.LessThanOrEqual(member, constant)
        };
    }

    private static Expression CaptureValue(object value, Type type)
    {
        // EF extracts captured values as parameters and reuses the query for different filter values.
        Expression<Func<object>> accessor = () => value;
        return Expression.Convert(accessor.Body, type);
    }

    private static object? ReadValue(JsonElement value, Type memberType)
    {
        var type = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (value.ValueKind == JsonValueKind.Null)
        {
            if (!memberType.IsValueType || Nullable.GetUnderlyingType(memberType) is not null) return null;
        }
        else if (type == typeof(string) && value.ValueKind == JsonValueKind.String) return value.GetString();
        else if (type == typeof(int) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var integer)) return integer;
        else if (type == typeof(long) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        else if (type.IsEnum && value.ValueKind == JsonValueKind.String)
        {
            var name = Enum.GetNames(type).FirstOrDefault(name => name.Equals(value.GetString(), StringComparison.OrdinalIgnoreCase));
            if (name is not null) return Enum.Parse(type, name);
        }
        else if (type == typeof(DateTimeOffset) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant) &&
                DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        }
        throw new FormatException($"Invalid filter value for {type.Name} field.");
    }
}
