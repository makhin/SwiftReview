using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ORP.Infrastructure.Persistence;
using ORP.Application.Grids;
using ORP.Domain.Messages;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class MessageGridOptionsTests
{
    private static MessageGridOptions Prepare(MessageGridRequest request) =>
        MessageGridOptions.Create(request);

    private static MessageReadRow Row(long id, string date, int branch = 1, string type = "MT199", string externalId = "TEST",
        string sender = "SENDER", string receiver = "RECEIVER", MessageDirection? direction = null) => new()
    {
        Id = id, ExternalId = externalId, MessageType = type, BranchId = branch, DepartmentIds = [1],
        BranchName = "Paris", DepartmentNames = ["CS"], CurrentAssigneeName = null,
        ActiveReviewerName = null, WorkflowDefinitionName = "Default",
        Sender = sender, Receiver = receiver,
        State = MessageState.New, ReceivedAt = DateTimeOffset.Parse(date), Direction = direction,
        CurrentAssigneeId = null, ActiveReviewId = null, ActiveReviewLevel = null, ActiveReviewerId = null,
        WorkflowDefinitionId = 1
    };

    [Fact]
    public void ScreenshotPayloadAndDefaults()
    {
        var request = JsonSerializer.Deserialize<MessageGridRequest>("""
            {"search":"","status":"","messageType":"","branch":null,"dateFrom":"","dateTo":"","page":1,"pageSize":20}
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var rows = Enumerable.Range(1, 25).Select(id => Row(id, "2026-10-09T12:00:00Z")).AsQueryable();
        Assert.Equal(25, Prepare(request).Filter(rows).Count());
        Assert.Equal(Enumerable.Range(1, 20).Select(id => (long)id), Prepare(request).Page(rows).Select(x => x.Id));
        Assert.Equal(25, Prepare(new(" ", " ", " ", null, " ", " ")).Filter(rows).Count());
        foreach (var json in new[] { "{\"skip\":0}", "{\"filter\":null}", "{\"page\":\"1\"}", "{\"branch\":\"1\"}", "{\"branch\":\"\"}", "{\"branch\":1.5}", "{\"branch\":2147483648}", "{\"departments\":[1]}", "{\"departments\":null}" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessageGridRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BranchAcceptsNumbersAndNull()
    {
        foreach (var json in new[] { "{}", "{\"branch\":null}" })
        {
            var request = JsonSerializer.Deserialize<MessageGridRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Null(request.Branch);
            Assert.Equal(2, Prepare(request).Filter(new[] { Row(1, "2026-10-09T12:00:00Z", branch: 1),
                Row(2, "2026-10-09T12:00:00Z", branch: 2) }.AsQueryable()).Count());
        }
        foreach (var branch in new[] { 1, int.MaxValue })
        {
            var request = JsonSerializer.Deserialize<MessageGridRequest>($"{{\"branch\":{branch}}}",
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var source = new[] { Row(1, "2026-10-09T12:00:00Z", branch), Row(2, "2026-10-09T12:00:00Z", 2) }.AsQueryable();
            Assert.Equal(branch, request.Branch);
            Assert.Equal(1, Assert.Single(Prepare(request).Filter(source)).Id);
        }
    }

    [Fact]
    public void DirectionCombinesWithOtherControlsBeforeCountingAndPaging()
    {
        const string date = "2026-10-09T12:00:00Z";
        var source = new[] { Row(3, date, direction: MessageDirection.Incoming),
            Row(1, date, direction: MessageDirection.Incoming), Row(2, date, direction: MessageDirection.Outgoing),
            Row(4, date), Row(5, date, branch: 2, direction: MessageDirection.Incoming),
            Row(6, date, type: "MT299", direction: MessageDirection.Incoming) }.AsQueryable();
        var request = new MessageGridRequest(Direction: " incoming ", Branch: 1, MessageType: "MT199", PageSize: 1);
        var options = Prepare(request);
        Assert.Equal(2, options.Filter(source).Count());
        Assert.Equal(1, Assert.Single(options.Page(options.Filter(source))).Id);
        var secondPage = Prepare(request with { Page = 2 });
        Assert.Equal(3, Assert.Single(secondPage.Page(secondPage.Filter(source))).Id);
        Assert.Equal(2, Assert.Single(Prepare(request with { Direction = "Outgoing" }).Filter(source)).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyDirectionDoesNotRestrictMessages(string? direction)
    {
        var request = JsonSerializer.Deserialize<MessageGridRequest>(
            JsonSerializer.Serialize(new { direction }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(2, Prepare(request).Filter(new[] { Row(1, "2026-10-09T12:00:00Z", branch: 1),
                Row(2, "2026-10-09T12:00:00Z", branch: 2) }.AsQueryable()).Count());
    }

    [Theory]
    [InlineData("Incoming", MessageDirection.Incoming)]
    [InlineData(" outgoing ", MessageDirection.Outgoing)]
    public void DirectionAcceptsEnumNamesInJson(string direction, MessageDirection expected)
    {
        var request = JsonSerializer.Deserialize<MessageGridRequest>(
            JsonSerializer.Serialize(new { direction }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var source = new[] { Row(1, "2026-10-09T12:00:00Z", direction: MessageDirection.Incoming),
            Row(2, "2026-10-09T12:00:00Z", direction: MessageDirection.Outgoing) }.AsQueryable();
        Assert.Equal(expected, Assert.Single(Prepare(request).Filter(source)).Direction);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("1")]
    [InlineData("Incoming,Outgoing")]
    public void InvalidDirectionIsRejected(string direction) =>
        Assert.Throws<FormatException>(() => Prepare(new(Direction: direction)));

    [Fact]
    public void NumericDirectionIsRejectedInJson() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessageGridRequest>(
            """{"direction":1}""", new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Fact]
    public void CombinedControlsDateBoundariesAndStablePaging()
    {
        var source = new[] { Row(3, "2026-10-09T23:59:59Z"), Row(2, "2026-10-01T00:00:00Z"),
            Row(1, "2026-10-01T00:00:00Z"), Row(4, "2026-10-10T00:00:00Z"), Row(5, "2026-10-09T12:00:00Z", branch: 2),
            Row(6, "2026-10-09T12:00:00Z", type: "MT299"), Row(7, "2026-09-30T23:59:59Z") }.AsQueryable();
        var request = new MessageGridRequest(Status: "new", MessageType: "MT199", Branch: 1, DateFrom: "2026-10-01", DateTo: "2026-10-09", PageSize: 2);
        var options = Prepare(request);
        Assert.Equal(3, options.Filter(source).Count());
        Assert.Equal(new long[] { 3, 1 }, options.Page(options.Filter(source)).Select(x => x.Id));
        var second = Prepare(request with { Page = 2 });
        Assert.Equal(new long[] { 2 }, second.Page(second.Filter(source)).Select(x => x.Id));
        Assert.Empty(Prepare(request with { Page = 3 }).Page(options.Filter(source)));
        Assert.Equal(3, Prepare(request with { PageSize = 1 }).Filter(source).Count());
        Assert.Empty(Prepare(request with { Branch = 3 }).Filter(source));
    }

    [Fact]
    public void SearchMatchesEitherFieldAndCombinesWithBranch()
    {
        var source = new[] { Row(1, "2026-10-09T12:00:00Z", externalId: "ABC"), Row(2, "2026-10-09T12:00:00Z", type: "ABC"),
            Row(3, "2026-10-09T12:00:00Z", branch: 2, type: "ABC"), Row(4, "2026-10-09T12:00:00Z") }.AsQueryable();
        Assert.Equal(new long[] { 1, 2 }, Prepare(new(Search: " ABC ", Branch: 1)).Filter(source).Select(x => x.Id));
    }

    [Theory]
    [InlineData("sender", "asc", 4, 1, 2, 3)]
    [InlineData("SENDER", "DESC", 3, 1, 2, 4)]
    [InlineData("receiver", "asc", 4, 3, 1, 2)]
    [InlineData("RECEIVER", "DESC", 1, 2, 3, 4)]
    public void SenderAndReceiverSortingUsesStablePaging(string field, string direction,
        long first, long second, long third, long fourth)
    {
        const string date = "2026-10-09T12:00:00Z";
        var source = new[] { Row(3, date, sender: "B", receiver: "C"),
            Row(2, date, sender: "A", receiver: "D"), Row(1, date, sender: "A", receiver: "D"),
            Row(4, date, sender: "", receiver: "") }.AsQueryable();
        var request = new MessageGridRequest(Sort: [new(field, direction)], PageSize: 2);
        Assert.Equal(new[] { first, second }, Prepare(request).Page(source).Select(x => x.Id));
        Assert.Equal(new[] { third, fourth }, Prepare(request with { Page = 2 }).Page(source).Select(x => x.Id));
    }

    [Fact]
    public void ExactTimestampBoundsRespectOffsets()
    {
        var source = new[] { Row(1, "2026-10-09T12:00:00Z"), Row(2, "2026-10-09T12:00:01Z") }.AsQueryable();
        Assert.Equal(new long[] { 1 }, Prepare(new(DateFrom: "2026-10-09T14:00:00+02:00", DateTo: "2026-10-09T12:00:00Z")).Filter(source).Select(x => x.Id));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    [InlineData(int.MaxValue, 500)]
    public void InvalidPaging(int page, int pageSize) => Assert.Throws<FormatException>(() => Prepare(new(Page: page, PageSize: pageSize)));

    [Fact]
    public void InvalidFiltersAndScopes()
    {
        MessageGridRequest[] invalid = [new(Status: "missing"), new(Status: "1"), new(Branch: 0), new(Branch: -1), new(DateFrom: "2026-02-30"), new(DateTo: "9999-12-31"),
            new(DateFrom: "2026-10-09T12:00:00"), new(DateFrom: "2026-10-10", DateTo: "2026-10-09"),
            new(Search: new string('x', 101)), new(AssignmentScope: "unknown"),
            new(AssignmentScope: "departments"), new(AssignmentScope: "assignable"), new(AssignmentScope: "")];
        foreach (var request in invalid) Assert.Throws<FormatException>(() => Prepare(request));
        foreach (var scope in new string?[] { null, "mine" }) Prepare(new(AssignmentScope: scope));
        Prepare(new(PageSize: 500));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: [new("unknown", "asc")])));
    }
    [Fact]
    public void FlatMessageControlsTranslateToParameterizedSql()
    {
        var compilations = 0;
        using var db = CreateContext(() => compilations++);
        string Sql(string search, int branch)
        {
            var options = MessageGridOptions.Create(new(Search: search, Status: "New", MessageType: "MT199",
                Branch: branch, DateFrom: "2026-10-01", DateTo: "2026-10-09", Page: 2, PageSize: 5));
            return options.Page(options.Filter(db.Set<MessageReadRow>())).ToQueryString();
        }
        var first = Sql("Alice", 1);
        var second = Sql("Bob", 2);
        Assert.Equal(1, compilations);
        Assert.Equal(Statement(first), Statement(second));
        Assert.Contains(" OR ", first);
        Assert.Contains("OFFSET", first);
        Assert.Contains("FETCH NEXT", first);
        Assert.DoesNotContain("Alice", Statement(first));
    }

    [Fact]
    public async Task AdminSearchLengthIsValidatedBeforeDatabaseAccess()
    {
        using var db = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        await Assert.ThrowsAsync<FormatException>(() => new AdminUserGridQueries(db)
            .LoadAsync(new(Search: new string('a', 101)), TestContext.Current.CancellationToken));
    }

    private static string Statement(string sql) => sql[sql.IndexOf("SELECT ", StringComparison.Ordinal)..];
    private static QueryContext CreateContext(Action compiled) => new(new DbContextOptionsBuilder<QueryContext>()
        .UseSqlServer("Server=localhost;Database=GridQueryParameterizationTests;Integrated Security=true")
        .EnableServiceProviderCaching(false)
        .LogTo(_ => compiled(), [CoreEventId.QueryCompilationStarting]).Options);
    private sealed class QueryContext(DbContextOptions<QueryContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<MessageReadRow>().HasKey(x => x.Id);
    }
}
