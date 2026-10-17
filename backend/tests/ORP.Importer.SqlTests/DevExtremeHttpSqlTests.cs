using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ORP.Domain.Assignments;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Microsoft.Extensions.Logging;
using ORP.Domain.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class DevExtremeHttpSqlTests
{
    [Fact]
    public async Task GridsKeepDevExtremeContractsActionsVisibilityAndBinaryPdf()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        foreach (var index in Enumerable.Range(1, 3))
        {
            var file = fixture.File($"body {index}", $"message-{index}.prt");
            await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        }
        await using (var db = new ORPDbContext(fixture.Options))
        {
            db.AddRange(new User("grid-admin", "Grid Admin", true), new User("grid-outsider", "Grid Outsider"));
            await db.SaveChangesAsync(ct);
            await db.SwiftMessages.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Sender, "TEST-SENDER")
                .SetProperty(x => x.Receiver, "TEST-RECEIVER"), ct);
        }
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "grid-admin");
        var query = "/api/messages/grid?skip=0&take=2&requireTotalCount=true";
        using var page = await GetJson(client, query, ct);
        Assert.Equal(3, page.RootElement.GetProperty("totalCount").GetInt32());
        var rows = page.RootElement.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        var first = rows[0];
        var id = first.GetProperty("id").GetInt64();
        Assert.Equal(JsonValueKind.Number, first.GetProperty("id").ValueKind);
        Assert.True(first.GetProperty("canAssign").GetBoolean());
        Assert.True(first.GetProperty("canViewAudit").GetBoolean());
        Assert.Equal("CS", first.GetProperty("departmentNames").GetString());
        Assert.Equal("ReviewSession", first.GetProperty("confirmationMode").GetString());
        var department = first.GetProperty("departmentIds")[0].GetInt32();
        using var filtered = await GetJson(client, query + "&departments=" + department +
            "&filter=" + Uri.EscapeDataString("[\"sender\",\"contains\",\"TEST\"]") +
            "&sort=" + Uri.EscapeDataString("[{\"selector\":\"receiver\",\"desc\":true}]"), ct);
        Assert.Equal(3, filtered.RootElement.GetProperty("totalCount").GetInt32());
        using var selected = await GetJson(client, query + "&select=" + Uri.EscapeDataString("[\"id\",\"version\",\"canAssign\",\"confirmationMode\"]"), ct);
        var selection = selected.RootElement.GetProperty("data")[0];
        Assert.Equal(4, selection.EnumerateObject().Count());
        Assert.True(selection.GetProperty("canAssign").GetBoolean());
        Assert.Equal("ReviewSession", selection.GetProperty("confirmationMode").GetString());
        using var grouped = await GetJson(client, query + "&requireGroupCount=true&group=" +
            Uri.EscapeDataString("[{\"selector\":\"state\",\"isExpanded\":true}]") +
            "&totalSummary=" + Uri.EscapeDataString("[{\"summaryType\":\"count\"}]"), ct);
        Assert.Equal(1, grouped.RootElement.GetProperty("groupCount").GetInt32());
        Assert.Equal(3, grouped.RootElement.GetProperty("summary")[0].GetInt32());
        Assert.Equal("ReviewSession", grouped.RootElement.GetProperty("data")[0].GetProperty("items")[0]
            .GetProperty("confirmationMode").GetString());
        using var count = await GetJson(client, "/api/messages/grid?skip=0&take=0&isCountQuery=true", ct);
        Assert.Equal(3, count.RootElement.GetProperty("totalCount").GetInt32());
        using var users = await GetJson(client, "/api/admin/users/grid?skip=0&take=1&search=Grid", ct);
        Assert.Equal(2, users.RootElement.GetProperty("totalCount").GetInt32());
        using var candidates = await GetJson(client, $"/api/messages/{id}/workflow-candidates", ct);
        Assert.Single(candidates.RootElement.EnumerateArray());
        using var pdf = await client.GetAsync($"/api/messages/{id}/pdf", ct);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", await pdf.Content.ReadAsStringAsync(ct));
        using var invalid = await client.GetAsync(query + "&filter=" + Uri.EscapeDataString("[\"departmentIds\",\"=\",1]"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var strict = await client.PostAsJsonAsync($"/api/messages/{id}/assign", new { assignedTo = "1" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, strict.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "grid-outsider");
        using var hidden = await GetJson(client, query + "&departments=" + department, ct);
        Assert.Equal(0, hidden.RootElement.GetProperty("totalCount").GetInt32());
        using var deniedPdf = await client.GetAsync($"/api/messages/{id}/pdf", ct);
        Assert.Equal(HttpStatusCode.NotFound, deniedPdf.StatusCode);
        using var deniedUsers = await client.GetAsync("/api/admin/users/grid", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedUsers.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GridKeepsMessageRejectionPolicyAfterRoutingChanges(bool rejectAllowed)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File();
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var user = new User("policy-reader", "Policy Reader");
            var role = new Role("Policy viewer");
            db.AddRange(user, role);
            await db.SaveChangesAsync(ct);
            var permission = await db.Permissions.SingleAsync(x => x.Name == Permissions.MessageView, ct);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id,
                BranchId = await db.Branches.Select(x => x.Id).SingleAsync(ct),
                DepartmentId = await db.Departments.Select(x => x.Id).SingleAsync(ct) });
            var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(ct);
            workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, ConfirmationActorMode.Assignee,
                false, AssignmentRetentionMode.PerConfirmation, rejectAllowed);
            (await db.DepartmentRoutingRules.SingleAsync(ct)).IsActive = false;
            await db.SaveChangesAsync(ct);
        }
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "policy-reader");
        using var workflows = await GetJson(client, "/api/workflows", ct);
        Assert.Empty(workflows.RootElement.EnumerateArray());
        using var grid = await GetJson(client, "/api/messages/grid?skip=0&take=20&select=" +
            Uri.EscapeDataString("[\"id\",\"rejectAllowed\"]"), ct);
        var row = Assert.Single(grid.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(rejectAllowed, row.GetProperty("rejectAllowed").GetBoolean());
    }

    [Fact]
    public async Task ConcurrentHttpConfirmationsWithOneVersionPersistOnlyOneApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File();
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        long id;
        string version;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var actor = new User("confirm-admin", "Confirm Admin", true);
            var assignee = new User("confirm-assignee", "Assignee");
            db.AddRange(actor, assignee);
            await db.SaveChangesAsync(ct);
            var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(ct);
            workflow.ConfigureConfirmation(ConfirmationMode.Immediate, ConfirmationActorMode.AnyAuthorizedUser,
                true, AssignmentRetentionMode.PerConfirmation, false);
            var message = await db.Messages.Include(x => x.Departments).SingleAsync(ct);
            message.Assign(assignee.Id);
            db.Assignments.Add(new Assignment(message.Id, actor.Id, assignee.Id, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
            id = message.Id;
            version = Convert.ToBase64String(message.RowVersion);
        }
        var gate = new ConfirmationGate();
        await using var factory = new Factory(fixture.ConnectionString, gate);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "confirm-admin");
        var request = new { level = 1, expectedVersion = version };
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/messages/{id}/reviews/confirm", request, ct),
            client.PostAsJsonAsync($"/api/messages/{id}/reviews/confirm", request, ct));
        Assert.True(gate.Arrivals >= 2);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(MessageState.Completed, (await verify.Messages.SingleAsync(ct)).State);
        Assert.Null((await verify.Messages.SingleAsync(ct)).CurrentAssigneeId);
        Assert.Equal(ReviewStatus.Approved, (await verify.Reviews.SingleAsync(ct)).Status);
        Assert.All(await verify.Assignments.ToListAsync(ct), assignment => Assert.NotNull(assignment.EndedAt));
    }

    private sealed class ConfirmationGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int Arrivals => arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) <= 2)
            {
                if (arrivals == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }

    private static async Task<JsonDocument> GetJson(HttpClient client, string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private sealed class Factory(string connection, SaveChangesInterceptor? interceptor = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection)
            .UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services =>
            {
                if (interceptor is not null)
                    services.AddDbContext<ORPDbContext>(options => options.AddInterceptors(interceptor));
            });
    }
}
