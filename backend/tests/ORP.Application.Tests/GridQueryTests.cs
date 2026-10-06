using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Schema;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using ORP.Domain.Messages;
using Xunit;

namespace ORP.Application.Tests;

public sealed class GridQueryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static GridFilter Condition(string field, string op, object? value) => new() { Field = field, Operator = op, Value = JsonSerializer.SerializeToElement(value) };
    private static GridFilter Group(string logic, params GridFilter?[] filters) => new() { Logic = logic, Filters = filters };
    private static GridQuery<MessageGridRowDto> Prepare(MessageGridRequest request) =>
        GridQuery<MessageGridRowDto>.Create(request, GridFields.Messages, 500, 5, new("receivedAt", "desc"));
    private static GridQuery<AdminUserGridRow> PrepareUsers(AdminUserGridRequest request) =>
        GridQuery<AdminUserGridRow>.Create(request, GridFields.Users, 100, 3, new("displayName", "asc"));
    private static MessageGridRowDto Row(long id = 1, int branch = 2, int department = 1, int? assignee = null,
        string externalId = "SWIFT-100", MessageState state = MessageState.Assigned,
        MessageDirection? direction = MessageDirection.Incoming, DateTimeOffset? receivedAt = null) => new()
    {
        Id = id, ExternalId = externalId, Direction = direction, MessageType = "MT199", BranchId = branch,
        DepartmentId = department, State = state, ReceivedAt = receivedAt ?? DateTimeOffset.Parse("2026-10-06T10:00:00+02:00"),
        CurrentAssigneeId = assignee, ActiveReviewId = null, ActiveReviewLevel = null, ActiveReviewerId = null,
        UndoReviewId = null, WorkflowDefinitionId = 1, CanReview = false, CanChangeWorkflow = false, RequiredReviewLevels = [1, 2]
    };

    [Theory]
    [InlineData("eq", 2, true)]
    [InlineData("ne", 2, false)]
    [InlineData("gt", 1, true)]
    [InlineData("gt", 2, false)]
    [InlineData("gte", 2, true)]
    [InlineData("lt", 3, true)]
    [InlineData("lt", 2, false)]
    [InlineData("lte", 2, true)]
    public void NumericComparisons(string op, int value, bool expected) =>
        Assert.Equal(expected, Prepare(new(Filter: Condition("branchId", op, value))).Filter(new[] { Row() }.AsQueryable()).Any());

    [Theory]
    [InlineData("contains", "IFT-1", true)]
    [InlineData("startsWith", "SWIFT", true)]
    [InlineData("endsWith", "100", true)]
    [InlineData("contains", "OTHER", false)]
    [InlineData("startsWith", "100", false)]
    [InlineData("endsWith", "SWIFT", false)]
    public void TextComparisons(string op, string value, bool expected)
    {
        var options = Prepare(new(Filter: Condition("externalId", op, value)));
        Assert.Equal(expected, options.Filter(new[] { Row() }.AsQueryable()).Any());
        Assert.Empty(options.Filter(new[] { Row(externalId: null!) }.AsQueryable()));
    }

    [Fact]
    public void NestedGroupsAndCaseInsensitiveTokens()
    {
        var filter = Group("AND", Condition("STATE", "EQ", "assigned"), Group("Or",
            Condition("branchId", "eq", 2), Condition("externalId", "Contains", "OTHER")));
        var source = new[] { Row(1), Row(2, branch: 3, externalId: "OTHER"), Row(3, branch: 4), Row(4, state: MessageState.Completed) }.AsQueryable();
        Assert.Equal(new long[] { 1, 2 }, Prepare(new(Filter: filter)).Filter(source).Select(row => row.Id));
    }

    [Fact]
    public void NullableComparisonsAndEnumValues()
    {
        var source = new[] { Row(1), Row(2, assignee: 5), Row(3, direction: null) }.AsQueryable();
        Assert.Equal(new long[] { 1, 3 }, Prepare(new(Filter: Condition("currentAssigneeId", "eq", null))).Filter(source).Select(row => row.Id));
        Assert.Equal(new long[] { 2 }, Prepare(new(Filter: Condition("currentAssigneeId", "ne", null))).Filter(source).Select(row => row.Id));
        Assert.Equal(new long[] { 2 }, Prepare(new(Filter: Condition("currentAssigneeId", "gt", 4))).Filter(source).Select(row => row.Id));
        Assert.Equal(new long[] { 1, 2 }, Prepare(new(Filter: Condition("direction", "eq", "Incoming"))).Filter(source).Select(row => row.Id));
        Assert.Equal(new long[] { 3 }, Prepare(new(Filter: Condition("direction", "eq", null))).Filter(source).Select(row => row.Id));
    }

    [Theory]
    [InlineData("eq", "2026-10-06T08:00:00Z", true)]
    [InlineData("ne", "2026-10-06T08:00:00Z", false)]
    [InlineData("gt", "2026-10-06T07:59:59Z", true)]
    [InlineData("gte", "2026-10-06T08:00:00Z", true)]
    [InlineData("lt", "2026-10-06T08:00:01+00:00", true)]
    [InlineData("lte", "2026-10-06T08:00:00.0000000Z", true)]
    public void DateComparisonsRespectOffsets(string op, string value, bool expected) =>
        Assert.Equal(expected, Prepare(new(Filter: Condition("receivedAt", op, value))).Filter(new[] { Row() }.AsQueryable()).Any());

    [Theory]
    [InlineData("branchId", "eq", "\"2\"")]
    [InlineData("branchId", "eq", "2.5")]
    [InlineData("branchId", "eq", "2147483648")]
    [InlineData("id", "eq", "9223372036854775808")]
    [InlineData("branchId", "eq", "null")]
    [InlineData("state", "eq", "\"Missing\"")]
    [InlineData("state", "eq", "\"1\"")]
    [InlineData("state", "eq", "1")]
    [InlineData("state", "gt", "\"Assigned\"")]
    [InlineData("branchId", "contains", "2")]
    [InlineData("externalId", "gt", "\"A\"")]
    [InlineData("externalId", "eq", "true")]
    [InlineData("externalId", "eq", "[]")]
    [InlineData("externalId", "eq", "{}")]
    [InlineData("externalId", "contains", "null")]
    [InlineData("receivedAt", "eq", "\"2026-10-06\"")]
    [InlineData("receivedAt", "eq", "\"2026-10-06T08:00:00\"")]
    [InlineData("receivedAt", "eq", "\"2026-02-30T08:00:00Z\"")]
    [InlineData("branchId", "in", "[2]")]
    public void RejectsInvalidTypesValuesAndOperators(string field, string op, string json) =>
        Assert.Throws<FormatException>(() => Prepare(new(Filter: new() { Field = field, Operator = op, Value = JsonSerializer.Deserialize<JsonElement>(json) })));

    [Theory]
    [InlineData("canReview")]
    [InlineData("canChangeWorkflow")]
    [InlineData("undoReviewId")]
    [InlineData("requiredReviewLevels")]
    [InlineData("rawContent")]
    [InlineData("sender")]
    public void RejectsFieldsOutsideAllowlist(string field)
    {
        Assert.Throws<FormatException>(() => Prepare(new(Filter: Condition(field, "eq", 1))));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: [new(field, "asc")])));
    }

    [Fact]
    public void SupportsEveryAllowedSortField()
    {
        foreach (var field in GridFields.Messages)
            Assert.Single(Prepare(new(Sort: [new(field.ToUpperInvariant(), "ASC")])).Page(new[] { Row() }.AsQueryable()));
        foreach (var field in GridFields.Users)
            Assert.Single(PrepareUsers(new(Sort: [new(field, "desc")])).Page(new[] { new AdminUserGridRow { Id = 1 } }.AsQueryable()));
        Assert.Single(Prepare(new(Filter: Condition("workflowDefinitionId", "eq", 1))).Filter(new[] { Row() }.AsQueryable()));
    }

    [Fact]
    public void RejectsMalformedFilterStructures()
    {
        GridFilter[] invalid = [new(), new() { Field = "id", Operator = "eq" }, new() { Logic = "and" }, Group("and"),
            Group("xor", Condition("id", "eq", 1)), Group("or", (GridFilter?)null),
            new() { Field = "id", Operator = "eq", Value = JsonSerializer.SerializeToElement(1), Logic = "and", Filters = [Condition("id", "eq", 1)] },
            new() { Logic = "and", Filters = [Condition("id", "eq", 1)], Value = JsonSerializer.SerializeToElement<object?>(null) }];
        foreach (var filter in invalid) Assert.Throws<FormatException>(() => Prepare(new(Filter: filter)));
    }

    [Fact]
    public void BoundsFilterDepthAndConditionCount()
    {
        var filter = Condition("id", "eq", 1);
        for (var i = 0; i < 8; i++) filter = Group("and", filter);
        Prepare(new(Filter: filter));
        Assert.Throws<FormatException>(() => Prepare(new(Filter: Group("or", filter))));
        Prepare(new(Filter: Group("or", Enumerable.Range(1, 64).Select(id => Condition("id", "eq", id)).ToArray())));
        Assert.Throws<FormatException>(() => Prepare(new(Filter: Group("or", Enumerable.Range(1, 65).Select(id => Condition("id", "eq", id)).ToArray()))));
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 501)]
    public void RejectsInvalidPaging(int skip, int take) => Assert.Throws<FormatException>(() => Prepare(new(skip, take)));

    [Fact]
    public void ValidatesEndpointSpecificLimitsAndSorts()
    {
        Prepare(new(Take: 500));
        PrepareUsers(new(Take: 100, Search: new string('a', 100)));
        Assert.Throws<FormatException>(() => PrepareUsers(new(Take: 101)));
        Assert.Throws<FormatException>(() => PrepareUsers(new(Search: new string('a', 101))));
        Assert.Throws<FormatException>(() => Prepare(new(AssignmentScope: "unknown")));
        foreach (var scope in new[] { MessageAssignmentScopes.Mine, MessageAssignmentScopes.Departments, MessageAssignmentScopes.Assignable })
            Prepare(new(AssignmentScope: scope));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: Enumerable.Repeat(new SortClause("id", "asc"), 6).ToArray())));
        Assert.Throws<FormatException>(() => PrepareUsers(new(Sort: Enumerable.Repeat(new SortClause("id", "asc"), 4).ToArray())));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: [new("id", "sideways")])));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: [new(null!, "asc")])));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: [new("id", null!)])));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: [null!])));
    }

    [Fact]
    public void DefaultsMultipleSortsAndExplicitIdProduceStablePaging()
    {
        var source = new[] { Row(4, department: 2), Row(2, department: 1), Row(3, department: 2), Row(1, branch: 3) }.AsQueryable();
        Assert.Equal(new long[] { 1, 2, 3, 4 }, Prepare(new()).Page(source).Select(row => row.Id));
        var options = Prepare(new(Skip: 1, Take: 2, Sort: [new("branchId", "asc"), new("departmentId", "desc")]));
        Assert.Equal(new long[] { 4, 2 }, options.Page(source).Select(row => row.Id));
        Assert.Equal(new long[] { 4, 3, 2, 1 }, Prepare(new(Sort: [new("ID", "DESC")])).Page(source).Select(row => row.Id));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, Prepare(new(Sort: [])).Page(source).Select(row => row.Id));
        var recent = new[] { Row(1), Row(2, receivedAt: DateTimeOffset.Parse("2026-10-06T11:00:00+02:00")) }.AsQueryable();
        Assert.Equal(new long[] { 2, 1 }, Prepare(new()).Page(recent).Select(row => row.Id));
    }

    [Fact]
    public void TotalCountIsFilteredBeforePagingAndEmptyPages()
    {
        var source = new[] { Row(1), Row(2), Row(3, branch: 3) }.AsQueryable();
        foreach (var request in new[] { new MessageGridRequest(Take: 1), new MessageGridRequest(Skip: 10), new MessageGridRequest(Take: 500) })
        {
            var options = Prepare(request with { Filter = Condition("branchId", "eq", 2) });
            var filtered = options.Filter(source);
            var result = new PagedResult<MessageGridRowDto>(options.Page(filtered).ToList(), filtered.Count());
            Assert.Equal(2, result.TotalCount);
            Assert.Equal(request.Skip == 10 ? 0 : Math.Min(request.Take, 2), result.Items.Count);
        }
        var emptyOptions = Prepare(new(Filter: Condition("branchId", "eq", 99)));
        Assert.Empty(emptyOptions.Page(emptyOptions.Filter(source)));
        Assert.Equal(0, emptyOptions.Filter(source).Count());
    }

    [Fact]
    public void UserGridSharesFilteringAndDefaultsToDisplayNameThenId()
    {
        var source = new[]
        {
            new AdminUserGridRow { Id = 3, UserName = "other", DisplayName = "Zoe" },
            new AdminUserGridRow { Id = 2, UserName = "test.two", DisplayName = "Amy" },
            new AdminUserGridRow { Id = 1, UserName = "test.one", DisplayName = "Amy" }
        }.AsQueryable();
        var options = PrepareUsers(new(Filter: Condition("userName", "startsWith", "test.")));
        Assert.Equal(new[] { 1, 2 }, options.Page(options.Filter(source)).Select(row => row.Id));
        Assert.Throws<FormatException>(() => PrepareUsers(new(Filter: Condition("isGlobalAdministrator", "eq", true))));
    }

    [Fact]
    public void FilterRoundTripsAndSchemasCanBeExported()
    {
        var filter = Group("and", Condition("state", "eq", "Assigned"), Condition("currentAssigneeId", "eq", null));
        var serialized = JsonSerializer.Serialize(filter, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<GridFilter>(serialized, JsonOptions)!;
        Assert.Single(Prepare(new(Filter: roundTrip)).Filter(new[] { Row() }.AsQueryable()));
        var schemaOptions = new JsonSerializerOptions(JsonOptions) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        foreach (var type in new[] { typeof(MessageGridRequest), typeof(AdminUserGridRequest), typeof(GridFilter) })
            Assert.NotNull(JsonSchemaExporter.GetJsonSchemaAsNode(schemaOptions.GetTypeInfo(type)));
    }

    [Fact]
    public void JsonContractPreservesDefaultsAndRejectsLegacyOrUnknownProperties()
    {
        var request = JsonSerializer.Deserialize<MessageGridRequest>("{}", JsonOptions)!;
        Assert.Equal(0, request.Skip);
        Assert.Equal(20, request.Take);
        Assert.Null(request.Filter);
        Prepare(request);
        var specified = JsonSerializer.Deserialize<MessageGridRequest>("""
            {"skip":3,"take":7,"assignmentScope":"mine","sort":[{"field":"id","direction":"desc"}],
             "filter":{"logic":"or","filters":[{"field":"state","operator":"eq","value":"Assigned"}]}}
            """, JsonOptions)!;
        Assert.Equal(3, specified.Skip);
        Assert.Equal(7, specified.Take);
        Assert.Equal("mine", specified.AssignmentScope);
        Prepare(specified);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessageGridRequest>("{\"group\":[]}", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessageGridRequest>("{\"take\":\"20\"}", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessageGridRequest>("{\"filter\":{\"selector\":\"id\"}}", JsonOptions));
        var users = JsonSerializer.Deserialize<AdminUserGridRequest>("{\"search\":\"Amy\"}", JsonOptions)!;
        Assert.Equal("Amy", users.Search);
        Assert.Equal(20, users.Take);
        var response = JsonSerializer.Serialize(new PagedResult<MessageGridRowDto>([Row()], 1), JsonOptions);
        using var document = JsonDocument.Parse(response);
        Assert.Equal("Assigned", document.RootElement.GetProperty("items")[0].GetProperty("state").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
    }
}
