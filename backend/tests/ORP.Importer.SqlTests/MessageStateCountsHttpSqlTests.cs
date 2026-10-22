using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class MessageStateCountsHttpSqlTests
{
    [Fact]
    public async Task CountsRespectAssignmentScopeAndMessageAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        for (var i = 0; i < 6; i++)
        {
            var file = fixture.File(name: $"counts-{i}.prt");
            await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        }
        int branchId;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            branchId = await db.Branches.Select(x => x.Id).SingleAsync(ct);
            var viewer = new User("counts-reader", "Reader");
            var other = new User("counts-other", "Other");
            var hiddenDepartment = new Department("Hidden");
            var role = new Role("Counts reader");
            role.Permissions.Add(new RolePermission
            {
                Permission = await db.Permissions.SingleAsync(x => x.Name == Permissions.MessageView, ct)
            });
            db.AddRange(viewer, other, hiddenDepartment, role);
            await db.SaveChangesAsync(ct);
            db.UserRoles.Add(new UserRole
            {
                UserId = viewer.Id, RoleId = role.Id,
                BranchId = await db.Branches.Select(x => x.Id).SingleAsync(ct),
                DepartmentId = await db.Departments.Where(x => x.Id != hiddenDepartment.Id).Select(x => x.Id).SingleAsync(ct)
            });
            var messages = await db.Messages.OrderBy(x => x.Id).ToListAsync(ct);
            var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(ct);
            // One unassigned, one assignee match, one reviewer match, one match on both,
            // one assigned to somebody else, and one inaccessible assignee match.
            messages[1].Assign(viewer.Id);
            messages[2].Assign(viewer.Id);
            db.Reviews.Add(messages[2].StartReview(1, viewer.Id, workflow, [], DateTimeOffset.UtcNow,
                isGlobalAdministrator: true));
            // Historical data may have an active reviewer distinct from the assignee.
            typeof(Message).GetProperty(nameof(Message.CurrentAssigneeId))!.SetValue(messages[2], other.Id);
            messages[3].Assign(viewer.Id);
            db.Reviews.Add(messages[3].StartReview(1, viewer.Id, workflow, [], DateTimeOffset.UtcNow,
                isGlobalAdministrator: true));
            messages[4].Assign(other.Id);
            messages[5].Assign(viewer.Id);
            await db.MessageDepartments.Where(x => x.MessageId == messages[5].Id).ExecuteDeleteAsync(ct);
            db.MessageDepartments.Add(new MessageDepartment(messages[5].Id, hiddenDepartment.Id));
            await db.SaveChangesAsync(ct);
        }
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "counts-reader");
        foreach (var (scope, expected) in new (string? Scope, int Count)[] { (null, 5), ("mine", 3) })
        {
            var url = "/api/messages/state-counts" + (scope is null ? "" : $"?assignmentScope={scope}");
            using var response = await client.GetAsync(url, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var counts = (await response.Content.ReadFromJsonAsync<MessageStateCountsDto>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }, ct))!;
            Assert.Equal(expected, counts.Total.Count);
            Assert.Equal(expected, counts.Items.Sum(x => x.Count));
            var type = Assert.Single(counts.Total.Breakdown);
            Assert.Equal("MT199", type.MessageType);
            Assert.Equal(expected, type.Count);
            Assert.Equal(scope is null ? 1 : 0, counts.Items.Where(x => x.State == MessageState.New).Sum(x => x.Count));
            Assert.Equal(scope is null ? 2 : 1, counts.Items.Single(x => x.State == MessageState.Assigned).Count);
            Assert.Equal(2, counts.Items.Single(x => x.State == MessageState.FirstReviewInProgress).Count);
            using var gridResponse = await client.PostAsJsonAsync("/api/messages/grid",
                new MessageGridRequest(Branch: scope is null ? null : branchId, AssignmentScope: scope), ct);
            Assert.Equal(HttpStatusCode.OK, gridResponse.StatusCode);
            using var grid = JsonDocument.Parse(await gridResponse.Content.ReadAsStringAsync(ct));
            Assert.Equal(expected, grid.RootElement.GetProperty("totalCount").GetInt32());
        }
        foreach (var scope in new[] { "departments", "assignable", "unknown", "", "null" })
        {
            using var response = await client.GetAsync($"/api/messages/state-counts?assignmentScope={scope}", ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var unsupportedGridFilter = await client.PostAsJsonAsync("/api/messages/grid", new { departments = new[] { 1 } }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, unsupportedGridFilter.StatusCode);
        using var otherBranch = await client.PostAsJsonAsync("/api/messages/grid", new MessageGridRequest(Branch: int.MaxValue), ct);
        Assert.Equal(HttpStatusCode.OK, otherBranch.StatusCode);
        using var emptyGrid = JsonDocument.Parse(await otherBranch.Content.ReadAsStringAsync(ct));
        Assert.Equal(0, emptyGrid.RootElement.GetProperty("totalCount").GetInt32());
        using var stringBranch = await client.PostAsJsonAsync("/api/messages/grid", new { branch = branchId.ToString() }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, stringBranch.StatusCode);
        using var invalidBranch = await client.PostAsJsonAsync("/api/messages/grid", new MessageGridRequest(Branch: 0), ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalidBranch.StatusCode);
        using var schemaResponse = await client.GetAsync("/openapi/v1.json", ct);
        using var schema = JsonDocument.Parse(await schemaResponse.Content.ReadAsStringAsync(ct));
        var gridProperties = schema.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("MessageGridRequest").GetProperty("properties");
        Assert.False(gridProperties.TryGetProperty("departments", out _));
        var operation = schema.RootElement.GetProperty("paths").GetProperty("/api/messages/state-counts").GetProperty("get");
        var parameter = Assert.Single(operation.GetProperty("parameters").EnumerateArray(),
            x => x.GetProperty("name").GetString() == "assignmentScope");
        Assert.Equal("query", parameter.GetProperty("in").GetString());
        Assert.False(parameter.TryGetProperty("required", out var required) && required.GetBoolean());
        Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
    }

    private sealed class Factory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }
}
