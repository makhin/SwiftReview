using System.Text.Json;
using ORP.Application.Grids;
using ORP.Domain.Messages;
using Xunit;

namespace ORP.Application.Tests;

public sealed class MessageGridOptionsTests
{
    private static GridQuery<MessageGridRowDto> Prepare(MessageGridRequest request) =>
        GridQuery<MessageGridRowDto>.Create(MessageGridOptions.Create(request), GridFields.Messages, 500, 5, new("receivedAt", "desc"));

    private static MessageGridRowDto Row(long id, string date, int branch = 1, string type = "MT199", string externalId = "TEST") => new()
    {
        Id = id, ExternalId = externalId, MessageType = type, BranchId = branch, DepartmentId = 1,
        State = MessageState.New, ReceivedAt = DateTimeOffset.Parse(date), Direction = null,
        CurrentAssigneeId = null, ActiveReviewId = null, ActiveReviewLevel = null, ActiveReviewerId = null,
        UndoReviewId = null, WorkflowDefinitionId = 1, CanReview = false, CanChangeWorkflow = false, RequiredReviewLevels = []
    };

    [Fact]
    public void ScreenshotPayloadAndDefaults()
    {
        var request = JsonSerializer.Deserialize<MessageGridRequest>("""
            {"search":"","status":"","messageType":"","branch":"","dateFrom":"","dateTo":"","page":1,"pageSize":20}
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(MessageGridOptions.Create(request).Filter);
        Assert.Equal(0, MessageGridOptions.Create(new()).Skip);
        Assert.Equal(20, MessageGridOptions.Create(new()).Take);
        Assert.Null(MessageGridOptions.Create(new(" ", " ", " ", " ", " ", " ")).Filter);
        foreach (var json in new[] { "{\"skip\":0}", "{\"filter\":null}", "{\"page\":\"1\"}", "{\"branch\":1}" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessageGridRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void CombinedControlsDateBoundariesAndStablePaging()
    {
        var source = new[] { Row(3, "2026-10-09T23:59:59Z"), Row(2, "2026-10-01T00:00:00Z"),
            Row(1, "2026-10-01T00:00:00Z"), Row(4, "2026-10-10T00:00:00Z"), Row(5, "2026-10-09T12:00:00Z", branch: 2),
            Row(6, "2026-10-09T12:00:00Z", type: "MT299"), Row(7, "2026-09-30T23:59:59Z") }.AsQueryable();
        var request = new MessageGridRequest(Status: "new", MessageType: "MT199", Branch: "1", DateFrom: "2026-10-01", DateTo: "2026-10-09", PageSize: 2);
        var options = Prepare(request);
        Assert.Equal(3, options.Filter(source).Count());
        Assert.Equal(new long[] { 3, 1 }, options.Page(options.Filter(source)).Select(x => x.Id));
        var second = Prepare(request with { Page = 2 });
        Assert.Equal(new long[] { 2 }, second.Page(second.Filter(source)).Select(x => x.Id));
        Assert.Empty(Prepare(request with { Page = 3 }).Page(options.Filter(source)));
        Assert.Equal(3, Prepare(request with { PageSize = 1 }).Filter(source).Count());
        Assert.Empty(Prepare(request with { Branch = "3" }).Filter(source));
    }

    [Fact]
    public void SearchMatchesEitherFieldAndCombinesWithBranch()
    {
        var source = new[] { Row(1, "2026-10-09T12:00:00Z", externalId: "ABC"), Row(2, "2026-10-09T12:00:00Z", type: "ABC"),
            Row(3, "2026-10-09T12:00:00Z", branch: 2, type: "ABC"), Row(4, "2026-10-09T12:00:00Z") }.AsQueryable();
        Assert.Equal(new long[] { 1, 2 }, Prepare(new(Search: " ABC ", Branch: "1")).Filter(source).Select(x => x.Id));
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
        MessageGridRequest[] invalid = [new(Status: "missing"), new(Status: "1"), new(Branch: "London"), new(Branch: "0"),
            new(Branch: "2147483648"), new(DateFrom: "2026-02-30"), new(DateTo: "9999-12-31"),
            new(DateFrom: "2026-10-09T12:00:00"), new(DateFrom: "2026-10-10", DateTo: "2026-10-09"),
            new(Search: new string('x', 101)), new(AssignmentScope: "unknown")];
        foreach (var request in invalid) Assert.Throws<FormatException>(() => Prepare(request));
        foreach (var scope in new[] { "mine", "departments", "assignable" }) Prepare(new(AssignmentScope: scope));
        Prepare(new(PageSize: 500));
        Assert.Throws<FormatException>(() => Prepare(new(Sort: [new("unknown", "asc")])));
    }
}
