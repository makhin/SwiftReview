using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SwiftReview.Application.Abstractions;
using SwiftReview.Domain.Messages;
using SwiftReview.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace SwiftReview.IntegrationTests;

public sealed class ApiWorkflowTests
{
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task SqlServer_BackendWorkflow_ContractsAndGrid_WorkEndToEnd()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") == "1",
            "Set RUN_INTEGRATION_TESTS=1 when a Docker-compatible runtime is available.");
        var ct = TestContext.Current.CancellationToken;
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").WithPassword("SwiftReview_Test_Passw0rd!").Build();
        await sql.StartAsync(ct);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:SwiftReview"] = sql.GetConnectionString(), ["BootstrapDatabase"] = "false" }));
            web.ConfigureServices(services =>
            {
                services.RemoveAll<SwiftReviewDbContext>();
                services.RemoveAll<DbContextOptions<SwiftReviewDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SwiftReviewDbContext>>();
                services.AddDbContext<SwiftReviewDbContext>(options => options.UseSqlServer(sql.GetConnectionString()));
            });
        });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var seeded = scope.ServiceProvider.GetRequiredService<SwiftReviewDbContext>();
            await seeded.Database.MigrateAsync(ct);
            Assert.Equal(75, await seeded.Messages.CountAsync(ct));
            Assert.Equal(75, await seeded.MessageRawData.CountAsync(ct));
            Assert.Equal(8, await seeded.WorkflowDefinitions.CountAsync(ct));
            Assert.False(await seeded.Messages.AnyAsync(m => !seeded.WorkflowDefinitions.Any(w =>
                w.Id == m.WorkflowDefinitionId && w.MessageType == m.MessageType && w.DepartmentId == m.OwningDepartmentId), ct));
            Assert.DoesNotContain(await seeded.Reviews.AsNoTracking().Where(x => x.Status == Domain.Reviews.ReviewStatus.Approved)
                .GroupBy(x => x.MessageId).Select(g => g.Select(x => x.ReviewerId).Count() != g.Select(x => x.ReviewerId).Distinct().Count()).ToListAsync(ct), x => x);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "supervisor");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json", ct)).StatusCode);
        Assert.Equal(75, (await client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary", ct))!.Total);
        Assert.Equal(8, (await client.GetFromJsonAsync<List<WorkflowSummaryDto>>("/api/workflows", ct))!.Count);
        Assert.Equal(6, (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me", ct)).StatusCode);

        var import = Request("IT-0001");
        var forbiddenImport = await client.PostAsJsonAsync("/api/messages/import", import, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenImport.StatusCode);
        Assert.Equal("application/problem+json", forbiddenImport.Content.Headers.ContentType?.MediaType);
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        var noWorkflow = await client.PostAsJsonAsync("/api/messages/import", import with
        {
            ExternalId = "IT-NO-WORKFLOW",
            DepartmentId = 2
        }, ct);
        Assert.Equal(HttpStatusCode.NotFound, noWorkflow.StatusCode);
        var created = await client.PostAsJsonAsync("/api/messages/import", import, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<Created>(ct))!.Id;
        var duplicate = await client.PostAsJsonAsync("/api/messages/import", import, ct);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "supervisor");

        var ineligible = await client.PostAsJsonAsync($"/api/messages/{id}/assign", new AssignMessageRequest(2), ct);
        Assert.Equal(HttpStatusCode.BadRequest, ineligible.StatusCode);
        var assigned = await client.PostAsJsonAsync($"/api/messages/{id}/assign", new AssignMessageRequest(6), ct);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/messages/{id}/reassign", new AssignMessageRequest(1), ct)).StatusCode);

        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "cs-reviewer");
        var started = await client.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new StartReviewRequest(1), ct);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var approved = await client.PostAsJsonAsync($"/api/messages/{id}/reviews/approve", new ApproveReviewRequest(1, "confirmed"), ct);
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        Assert.Equal(MessageState.Completed, (await client.GetFromJsonAsync<MessageDetailsDto>($"/api/messages/{id}", ResponseJson, ct))!.State);

        var search = await client.PostAsJsonAsync("/api/messages/search", new MessageSearchRequest(0, 10,
            [new SortClause("receivedAt", "desc")], new MessageFilter([MessageState.Completed], [1], ["MT199"], [1], null, null, "IT", "EUR")), ct);
        search.EnsureSuccessStatusCode(); Assert.True((await search.Content.ReadFromJsonAsync<PagedResult<MessageListItemDto>>(ResponseJson, ct))!.TotalCount >= 1);
        var page = await client.PostAsJsonAsync("/api/messages/search", new MessageSearchRequest(0, 1, null, null), ct);
        Assert.Single((await page.Content.ReadFromJsonAsync<PagedResult<MessageListItemDto>>(ResponseJson, ct))!.Items);
        var multiSort = await client.PostAsJsonAsync("/api/messages/search", new MessageSearchRequest(0, 20,
            [new SortClause("messageType", "asc"), new SortClause("receivedAt", "desc")], null), ct);
        var multiSortItems = (await multiSort.Content.ReadFromJsonAsync<PagedResult<MessageListItemDto>>(ResponseJson, ct))!.Items;
        Assert.Equal(multiSortItems.OrderBy(x => x.MessageType).ThenByDescending(x => x.ReceivedAt).ThenBy(x => x.Id).Select(x => x.Id),
            multiSortItems.Select(x => x.Id));
        await VerifyGrid(client, ct);

        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "tfo-reviewer");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/messages/{id}", ct)).StatusCode);
        using (var scopedGrid = await client.GetAsync("/api/messages/grid?skip=0&take=50", ct))
        {
            scopedGrid.EnsureSuccessStatusCode();
            using var scopedJson = JsonDocument.Parse(await scopedGrid.Content.ReadAsStringAsync(ct));
            var rows = scopedJson.RootElement.GetProperty("data").EnumerateArray().ToList();
            Assert.NotEmpty(rows);
            Assert.All(rows, row => { Assert.Equal(2, row.GetProperty("branchId").GetInt32()); Assert.Equal(2, row.GetProperty("departmentId").GetInt32()); });
        }
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "supervisor");
        var audit = await client.GetFromJsonAsync<List<AuditEventDto>>($"/api/messages/{id}/audit", ct);
        Assert.Contains(audit!, x => x.EventType == "MessageImported"); Assert.Contains(audit!, x => x.EventType == "MessageReassigned");
        Assert.Contains(audit!, x => x.EventType == "ReviewApproved");
        Assert.Contains(audit!, x => x.EventType == "MessageCompleted");

        await VerifyLevelPermission(client, ct);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SwiftReviewDbContext>();
            Assert.True(await db.OutboxMessages.CountAsync(x => x.ProcessedAt == null, ct) >= 4);
        }
    }

    private static ImportMessageRequest Request(string externalId) => new(externalId, "MT199", 1, 1,
        new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero), "BANKA", "BANKB", "IT-ACCOUNT", "EUR", 1200, "IT-REF", "{1:F01TEST}");

    private static async Task VerifyLevelPermission(HttpClient client, CancellationToken ct)
    {
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        var created = await client.PostAsJsonAsync("/api/messages/import", new ImportMessageRequest("IT-PERM-1", "MT760", 3, 3,
            DateTimeOffset.UtcNow, "BANKA", "BANKB", null, "USD", 50, null, "raw"), ct);
        var id = (await created.Content.ReadFromJsonAsync<Created>(ct))!.Id;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/messages/{id}/assign", new AssignMessageRequest(5), ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "supervisor");
        var start = await client.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new StartReviewRequest(1), ct);
        Assert.True(start.StatusCode == HttpStatusCode.Created, await start.Content.ReadAsStringAsync(ct));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/messages/{id}/reviews/approve", new ApproveReviewRequest(1, null), ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "dc-reviewer");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new StartReviewRequest(2), ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "supervisor");
    }

    private static async Task VerifyGrid(HttpClient client, CancellationToken ct)
    {
        const string filterValue = "[[\"currency\",\"=\",\"EUR\"],\"and\",[\"externalId\",\"contains\",\"IT\"]]";
        const string sortValue = "[{\"selector\":\"receivedAt\",\"desc\":true}]";
        var summary = Uri.EscapeDataString("[{\"selector\":\"amount\",\"summaryType\":\"sum\"}]");
        var filter = Uri.EscapeDataString(filterValue);
        var sort = Uri.EscapeDataString(sortValue);
        using var page = await client.GetAsync($"/api/messages/grid?skip=0&take=5&requireTotalCount=true&filter={filter}&sort={sort}&totalSummary={summary}", ct);
        var pageBody = await page.Content.ReadAsStringAsync(ct);
        Assert.True(page.IsSuccessStatusCode, pageBody);
        using var pageJson = JsonDocument.Parse(pageBody);
        Assert.True(pageJson.RootElement.GetProperty("data").GetArrayLength() >= 1);
        Assert.True(pageJson.RootElement.GetProperty("totalCount").GetInt32() >= 1);
        Assert.Equal(1, pageJson.RootElement.GetProperty("summary").GetArrayLength());

        var group = Uri.EscapeDataString("[{\"selector\":\"state\",\"isExpanded\":false}]");
        using var grouped = await client.GetAsync($"/api/messages/grid?skip=0&take=5&requireGroupCount=true&group={group}", ct);
        var groupedBody = await grouped.Content.ReadAsStringAsync(ct);
        Assert.True(grouped.IsSuccessStatusCode, groupedBody);
        using var groupJson = JsonDocument.Parse(groupedBody);
        Assert.True(groupJson.RootElement.GetProperty("data").GetArrayLength() >= 1);
        Assert.True(groupJson.RootElement.GetProperty("groupCount").GetInt32() >= 1);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/messages/grid?skip=0&take=501", ct)).StatusCode);
        var invalid = Uri.EscapeDataString("[{\"selector\":\"rawContent\",\"desc\":false}]");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/messages/grid?skip=0&take=10&sort={invalid}", ct)).StatusCode);
    }
    private sealed record Created(long Id);
}
