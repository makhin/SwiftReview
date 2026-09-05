using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class MockDataModeTests
{
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task DebugAuthentication_RejectsMissingAndUnknownUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", ct)).StatusCode);

        client.DefaultRequestHeaders.Add("X-Debug-User", "unknown-user");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", ct)).StatusCode);
    }

    [Fact]
    public async Task DebugAuthentication_IsDisabledOutsideDevelopment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", ct)).StatusCode);
    }

    [Fact]
    public async Task Api_StartsWithoutSqlServer_AndServesSeededGridData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Development");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");

        using var response = await client.GetAsync("/api/messages/grid?skip=0&take=10&requireTotalCount=true", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(10, json.RootElement.GetProperty("data").GetArrayLength());
        Assert.Equal(75, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.All(json.RootElement.GetProperty("data").EnumerateArray(), row =>
            Assert.StartsWith("MSG-", row.GetProperty("externalId").GetString()));
        Assert.All(json.RootElement.GetProperty("data").EnumerateArray(), row =>
            Assert.False(row.TryGetProperty("body", out _)));

        var details = await client.GetFromJsonAsync<MessageDetailsDto>("/api/messages/1", ResponseJson, ct);
        Assert.StartsWith("{1:F01MOCK", details!.Body);

        Assert.Equal(3, (await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/branches", ct))!.Count);
        Assert.Equal(3, (await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/departments", ct))!.Count);
        Assert.Equal(8, (await client.GetFromJsonAsync<List<string>>("/api/message-types", ct))!.Count);
        var states = (await client.GetFromJsonAsync<List<MessageStateReferenceDto>>("/api/message-states", ct))!;
        Assert.Equal(9, states.Count);
        Assert.Contains(new MessageStateReferenceDto("WaitingForSecondReview", "Waiting for second review"), states);
        Assert.Equal(15, (await client.GetFromJsonAsync<List<WorkflowSummaryDto>>("/api/workflows", ct))!
            .Sum(x => x.Steps.Count));
        var users = (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!;
        Assert.Equal(11, users.Count);
        Assert.Contains(users, x => x.UserName == "amelia.hart" && x.DisplayName == "Amelia Hart");
        Assert.Contains(users, x => x.UserName == "elena.petrova" && x.DisplayName == "Elena Petrova");
        Assert.DoesNotContain(users, x => x.DisplayName.Contains("Reviewer", StringComparison.OrdinalIgnoreCase));

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "amelia.hart");
        Assert.Equal([new ReferenceItemDto(1, "London")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/branches", ct));
        Assert.Equal([new ReferenceItemDto(1, "CS")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/departments", ct));
        Assert.Equal(["MT199", "MT700", "MT799"],
            await client.GetFromJsonAsync<List<string>>("/api/message-types", ct));
        Assert.Equal([5, 1],
            (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!.Select(x => x.Id));
        Assert.Contains(
            (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!,
            x => x.UserName == "admin" && x.DepartmentIds.SequenceEqual([2]));

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "5");
        var currentUser = await client.GetFromJsonAsync<CurrentUserResponse>("/api/me", ct);
        Assert.Equal(5, currentUser!.UserId);
        Assert.Equal("admin", currentUser.UserName);
    }

    [Fact]
    public async Task AuditTrail_IsPagedTypedAndPermissionScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");

        var registered = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/api/messages/1/audit?skip=0&take=1", ResponseJson, ct);
        Assert.Equal(1, registered!.TotalCount);
        var registration = Assert.Single(registered.Items);
        Assert.Equal(AuditEventType.MessageRegistered, registration.EventType);
        Assert.Null(registration.Actor);
        Assert.Equal(1, registration.Details.WorkflowDefinitionId);

        using (var assign = new HttpRequestMessage(HttpMethod.Post, "/api/messages/1/assign")
        {
            Content = JsonContent.Create(new AssignMessageRequest(1))
        })
        {
            assign.Headers.Add("X-Correlation-ID", "assign-1");
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(assign, ct)).StatusCode);
        }

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "amelia.hart");
        var start = await client.PostAsJsonAsync("/api/messages/1/reviews/start", new StartReviewRequest(1), ct);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var reviewId = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(cancellationToken: ct))!.ReviewId;
        using (var approve = new HttpRequestMessage(HttpMethod.Post, "/api/messages/1/reviews/approve")
        {
            Content = JsonContent.Create(new ApproveReviewRequest(1, "confirmed"))
        })
        {
            approve.Headers.Add("X-Correlation-ID", "approve-1");
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(approve, ct)).StatusCode);
        }

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        var latest = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/api/messages/1/audit?skip=0&take=2", ResponseJson, ct);
        Assert.Equal(5, latest!.TotalCount);
        Assert.Equal([AuditEventType.MessageCompleted, AuditEventType.ReviewApproved],
            latest.Items.Select(x => x.EventType));
        Assert.All(latest.Items, item =>
        {
            Assert.Equal(reviewId, item.Details.ReviewId);
            Assert.Equal(1, item.Details.ReviewLevel);
            Assert.Equal("confirmed", item.Details.Comment);
            Assert.Equal("amelia.hart", item.Actor!.UserName);
            Assert.Equal("approve-1", item.CorrelationId);
        });

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/messages/1/audit?take=0", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/messages/999999/audit", ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "amelia.hart");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/messages/1/audit", ct)).StatusCode);
    }

    [Fact]
    public async Task CorrelationId_RejectsValuesThatCannotBePersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", new string('x', 101));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request, ct)).StatusCode);
    }

    [Fact]
    public async Task AuditTrail_RecordsRejectAndUndoWithReviewContext()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/2/assign", new AssignMessageRequest(2), ct)).StatusCode);
        var startedForUndo = await client.PostAsJsonAsync(
            "/api/messages/2/reviews/start", new StartReviewRequest(1), ct);
        var undoReviewId = (await startedForUndo.Content.ReadFromJsonAsync<StartReviewResponse>(cancellationToken: ct))!.ReviewId;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/2/reviews/approve", new ApproveReviewRequest(1, "undo this"), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/2/undo", new UndoReviewRequest(undoReviewId), ct)).StatusCode);
        var undoAudit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/api/messages/2/audit", ResponseJson, ct);
        var undone = Assert.Single(undoAudit!.Items,
            x => x.EventType == AuditEventType.ConfirmationUndone);
        Assert.Equal(undoReviewId, undone.Details.ReviewId);
        Assert.Equal(1, undone.Details.ReviewLevel);
        Assert.Equal("undo this", undone.Details.Comment);

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/assign", new AssignMessageRequest(3), ct)).StatusCode);
        var startedForReject = await client.PostAsJsonAsync(
            "/api/messages/3/reviews/start", new StartReviewRequest(1), ct);
        var rejectReviewId = (await startedForReject.Content.ReadFromJsonAsync<StartReviewResponse>(cancellationToken: ct))!.ReviewId;
        var beforeFailedReject = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/api/messages/3/audit", ResponseJson, ct);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/reject", new RejectReviewRequest(1, ""), ct)).StatusCode);
        var afterFailedReject = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/api/messages/3/audit", ResponseJson, ct);
        Assert.Equal(beforeFailedReject!.TotalCount, afterFailedReject!.TotalCount);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/reject", new RejectReviewRequest(1, "invalid data"), ct)).StatusCode);
        var rejectAudit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/api/messages/3/audit", ResponseJson, ct);
        var rejected = Assert.Single(rejectAudit!.Items,
            x => x.EventType == AuditEventType.ReviewRejected);
        Assert.Equal(rejectReviewId, rejected.Details.ReviewId);
        Assert.Equal(1, rejected.Details.ReviewLevel);
        Assert.Equal("invalid data", rejected.Details.Comment);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment)
    {
        var factory = new WebApplicationFactory<Program>();
        return factory.WithWebHostBuilder(web =>
        {
            web.UseEnvironment(environment);
            web.UseSetting("UseMockData", "true");
            web.UseSetting("ConnectionStrings:ORP", "not-a-sql-server-connection");
        });
    }
}
