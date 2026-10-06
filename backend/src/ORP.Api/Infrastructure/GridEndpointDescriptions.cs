namespace ORP.Api.Infrastructure;

internal static class GridEndpointDescriptions
{
    private const string Common = """
        Send a JSON body; the response contains items and totalCount (after authorization/filtering, before paging).
        skip defaults to 0 and must be non-negative; take defaults to 20.
        sort is an ordered array of {field, direction: asc|desc}; id asc is appended unless id is already specified.
        filter is null/omitted, a condition {field, operator, value}, or a non-empty group {logic: and|or, filters: [...]}.
        Groups may be nested to depth 8 (root depth 0), with at most 64 conditions; group and condition properties cannot be mixed.
        Operators: eq/ne for all allowed fields (null only for nullable fields/strings);
        gt/gte/lt/lte for integers and dates; contains/startsWith/endsWith for non-null string values.
        Field names, operators, group logic and sort directions are case-insensitive.
        Values use the column's JSON type: integers, enum names, strings, or ISO 8601 timestamps with seconds and Z/offset.
        String matching follows SQL Server collation. Invalid options or unknown request/filter properties return 400 ProblemDetails.
        Grouping, summaries and selected-column responses are not supported.
        """;

    public const string Messages = Common + """

        take is limited to 500, with up to 5 sort clauses; default order is receivedAt desc, id asc.
        Allowed fields: id, externalId, direction, messageType, branchId, departmentId, state, receivedAt,
        currentAssigneeId, activeReviewId, activeReviewLevel, activeReviewerId, workflowDefinitionId.
        assignmentScope is optional: mine, departments, or assignable (requires assignment permission).
        Client filtering never expands branch/department access. Complete rows retain action flags and required review levels.
        """;

    public const string Users = Common + """

        GlobalAdministrator authorization is required. take is limited to 100, with up to 3 sort clauses;
        default order is displayName asc, id asc. Allowed fields: id, userName, displayName.
        search is optional, at most 100 characters, trimmed and matched against userName or displayName;
        search and filter are combined with AND.
        """;
}
