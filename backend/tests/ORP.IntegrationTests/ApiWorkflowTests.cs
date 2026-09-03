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
using ORP.Application.Abstractions;
using ORP.Domain.Messages;
using ORP.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace ORP.IntegrationTests;

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
        await using var sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").WithPassword("ORP_Test_Passw0rd!").Build();
        await sql.StartAsync(ct);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:ORP"] = sql.GetConnectionString(), ["BootstrapDatabase"] = "false", ["UseMockData"] = "false" }));
            web.ConfigureServices(services =>
            {
                services.RemoveAll<ORPDbContext>();
                services.RemoveAll<DbContextOptions<ORPDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ORPDbContext>>();
                services.AddDbContext<ORPDbContext>(options => options.UseSqlServer(sql.GetConnectionString()));
            });
        });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var seeded = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            await seeded.Database.MigrateAsync(ct);
            Assert.Equal(75, await seeded.Messages.CountAsync(ct));
            Assert.Equal(0, await seeded.SwiftMessageSource.CountAsync(ct));
            Assert.True(await seeded.Reviews.AnyAsync(ct));
            Assert.Equal(2, (await seeded.Database.GetAppliedMigrationsAsync(ct)).Count());
            var historyTableCount = await seeded.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id = t.schema_id WHERE t.name = N'__EFMigrationsHistory' AND s.name = N'dbo'")
                .SingleAsync(ct);
            Assert.Equal(1, historyTableCount);
            await CreateAndSeedSwiftSource(seeded, ct);
            Assert.Equal(76, await seeded.Messages.CountAsync(ct));
            Assert.Equal(76, await seeded.SwiftMessageSource.CountAsync(ct));
            Assert.Equal(8, await seeded.WorkflowDefinitions.CountAsync(ct));
            Assert.False(await seeded.Messages.AnyAsync(m => !seeded.WorkflowDefinitions.Any(w => w.Id == m.WorkflowDefinitionId), ct));
            Assert.DoesNotContain(await seeded.Reviews.AsNoTracking().Where(x => x.Status == Domain.Reviews.ReviewStatus.Approved)
                .GroupBy(x => x.MessageId).Select(g => g.Select(x => x.ReviewerId).Count() != g.Select(x => x.ReviewerId).Distinct().Count()).ToListAsync(ct), x => x);
            var forbiddenSourceWrite = new SwiftMessageRecord { MessageId = 2000, ExternalId = "FORBIDDEN", MessageType = "MT199", BranchId = 1, DepartmentId = 1, ReceivedAt = DateTimeOffset.UtcNow, Sender = "A", Receiver = "B" };
            seeded.SwiftMessageSource.Add(forbiddenSourceWrite);
            await Assert.ThrowsAsync<InvalidOperationException>(() => seeded.SaveChangesAsync(ct));
            seeded.Entry(forbiddenSourceWrite).State = EntityState.Detached;
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "supervisor");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json", ct)).StatusCode);
        Assert.Equal(76, (await client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary", ct))!.Total);
        Assert.Equal(8, (await client.GetFromJsonAsync<List<WorkflowSummaryDto>>("/api/workflows", ct))!.Count);
        Assert.Equal(6, (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!.Count);
        Assert.Equal(3, (await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/branches", ct))!.Count);
        Assert.Equal(3, (await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/departments", ct))!.Count);
        Assert.Equal(8, (await client.GetFromJsonAsync<List<string>>("/api/message-types", ct))!.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me", ct)).StatusCode);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/api/messages/import", null, ct)).StatusCode);
        const long id = 1001;

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
        Assert.Equal([new ReferenceItemDto(2, "Dublin")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/branches", ct));
        Assert.Equal([new ReferenceItemDto(2, "TFO")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/departments", ct));
        Assert.Equal(["MT299", "MT710", "MT999"],
            await client.GetFromJsonAsync<List<string>>("/api/message-types", ct));
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
        Assert.Contains(audit!, x => x.EventType == "MessageReassigned");
        Assert.Contains(audit!, x => x.EventType == "ReviewApproved");
        Assert.Contains(audit!, x => x.EventType == "MessageCompleted");

        await VerifyLevelPermission(client, ct);

    }

    private static async Task VerifyLevelPermission(HttpClient client, CancellationToken ct)
    {
        const long id = 6;
        client.DefaultRequestHeaders.Remove("X-Debug-User"); client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
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
    private static async Task CreateAndSeedSwiftSource(ORPDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("DROP VIEW IF EXISTS [ORP].[SwiftMessageSource];", ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE [ORP].[SwiftMessageSource]
            (
                [MessageID] bigint NOT NULL PRIMARY KEY,
                [ExternalId] nvarchar(100) NOT NULL,
                [MessageType] nvarchar(20) NOT NULL,
                [BranchId] int NOT NULL,
                [DepartmentId] int NOT NULL,
                [ReceivedAt] datetimeoffset NOT NULL,
                [Sender] nvarchar(100) NOT NULL,
                [Receiver] nvarchar(100) NOT NULL,
                [Account] nvarchar(100) NULL,
                [Currency] nvarchar(3) NULL,
                [Amount] decimal(19,4) NULL,
                [Reference] nvarchar(200) NULL
            );
            """, ct);
        for (var i = 1; i <= 75; i++)
        {
            string[] messageTypes = ["MT199", "MT299", "MT671", "MT700", "MT710", "MT760", "MT799", "MT999"];
            var typeIndex = (i - 1) % messageTypes.Length;
            await InsertSwiftMessage(db, i, $"SEED-{i:0000}", messageTypes[typeIndex],
                (i - 1) % 3 + 1, typeIndex % 3 + 1, ct);
        }
        await InsertSwiftMessage(db, 1001, "IT-0001", "MT199", 1, 1, ct);
        await db.Database.ExecuteSqlRawAsync("EXEC [ORP].[RegisterNewMessages];", ct);
        await db.Database.ExecuteSqlRawAsync("EXEC [ORP].[RegisterNewMessages];", ct);
        Assert.Equal(1, await db.Messages.CountAsync(x => x.Id == 1001, ct));
    }

    private static Task<int> InsertSwiftMessage(ORPDbContext db, long id, string externalId,
        string messageType, int branchId, int departmentId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [ORP].[SwiftMessageSource]
                ([MessageID], [ExternalId], [MessageType], [BranchId], [DepartmentId], [ReceivedAt], [Sender], [Receiver], [Account], [Currency], [Amount], [Reference])
            VALUES
                ({id}, {externalId}, {messageType}, {branchId}, {departmentId}, {new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero)}, {"A"}, {"B"}, {"IT-ACCOUNT"}, {"EUR"}, {1200m}, {"IT-REF"});
            """, ct);
}
