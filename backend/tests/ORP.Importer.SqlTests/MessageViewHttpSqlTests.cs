using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class MessageViewHttpSqlTests
{
    [Fact]
    public async Task ReturnsFullCardAndCurrentScopedRolesWithoutLeakingOtherScopes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File("  raw body\n:20:exact", "view-message.prt");
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        long id;
        string body;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var viewer = new User("card-reader", "Reader");
            var noAudit = new User("card-no-audit", "No audit");
            var outsideBranch = new User("card-other-branch", "Other branch");
            var outsideDepartment = new User("card-other-department", "Other department");
            var actor = new User("card-actor", "Actor");
            var wrongAuditScope = new User("card-wrong-audit", "Wrong audit scope");
            var branch = await db.Branches.SingleAsync(ct);
            var department = await db.Departments.SingleAsync(ct);
            var hiddenBranch = new Branch("Hidden branch");
            var hiddenDepartment = new Department("Hidden department");
            var read = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageView, ct);
            var audit = await db.Permissions.SingleAsync(p => p.Name == Permissions.AuditView, ct);
            var readRole = new Role("Read only");
            var auditRole = new Role("Audit reader");
            var firstRole = new Role("A role");
            var lastRole = new Role("Z role");
            var hiddenRole = new Role("Hidden role");
            readRole.Permissions.Add(new RolePermission { Permission = read });
            auditRole.Permissions.Add(new RolePermission { Permission = read });
            auditRole.Permissions.Add(new RolePermission { Permission = audit });
            db.AddRange(viewer, noAudit, outsideBranch, outsideDepartment, actor, wrongAuditScope, hiddenBranch, hiddenDepartment,
                readRole, auditRole, firstRole, lastRole, hiddenRole);
            await db.SaveChangesAsync(ct);
            db.UserRoles.AddRange(
                new UserRole { UserId = wrongAuditScope.Id, BranchId = branch.Id, DepartmentId = department.Id, RoleId = readRole.Id },
                new UserRole { UserId = wrongAuditScope.Id, BranchId = branch.Id, DepartmentId = hiddenDepartment.Id, RoleId = auditRole.Id },
                new UserRole { UserId = viewer.Id, BranchId = branch.Id, DepartmentId = department.Id, RoleId = auditRole.Id },
                new UserRole { UserId = noAudit.Id, BranchId = branch.Id, DepartmentId = department.Id, RoleId = readRole.Id },
                new UserRole { UserId = outsideBranch.Id, BranchId = hiddenBranch.Id, DepartmentId = department.Id, RoleId = auditRole.Id },
                new UserRole { UserId = outsideDepartment.Id, BranchId = branch.Id, DepartmentId = hiddenDepartment.Id, RoleId = auditRole.Id },
                new UserRole { UserId = actor.Id, BranchId = branch.Id, DepartmentId = department.Id, RoleId = lastRole.Id },
                new UserRole { UserId = actor.Id, BranchId = branch.Id, DepartmentId = department.Id, RoleId = firstRole.Id },
                new UserRole { UserId = actor.Id, BranchId = hiddenBranch.Id, DepartmentId = department.Id, RoleId = hiddenRole.Id },
                new UserRole { UserId = actor.Id, BranchId = branch.Id, DepartmentId = hiddenDepartment.Id, RoleId = hiddenRole.Id });
            var message = await db.Messages.SingleAsync(ct);
            message.Assign(actor.Id);
            id = message.Id;
            body = await db.SwiftMessages.Select(m => m.Body).SingleAsync(ct);
            for (var i = 0; i < 505; i++)
                db.AuditEvents.Add(new AuditEvent(id, AuditEventType.MessageAssigned, actor.Id,
                    DateTimeOffset.Parse("2026-10-19T10:00:00Z").AddSeconds(i / 2), null, null,
                    JsonSerializer.Serialize(new { assigneeId = actor.Id }), "card-test"));
            await db.SaveChangesAsync(ct);
        }
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "card-reader");
        using var response = await client.GetAsync($"/api/messages/{id}/view", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        Assert.Equal(id.ToString(), root.GetProperty("id").GetString());
        Assert.Equal(body, root.GetProperty("rawText").GetString());
        Assert.Equal("Incoming", root.GetProperty("direction").GetString());
        Assert.Equal("CS", root.GetProperty("department").GetString());
        var approval = root.GetProperty("approvals")[0];
        Assert.Equal("Actor", approval.GetProperty("assignee").GetString());
        Assert.Equal("A role, Z role", approval.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, approval.GetProperty("at").ValueKind);
        var history = root.GetProperty("auditLog").EnumerateArray().ToArray();
        Assert.Equal(506, history.Length);
        Assert.Contains(history, e => e.GetProperty("user").GetString() == "System" && e.GetProperty("role").GetString() == "System");
        Assert.All(history.Where(e => e.GetProperty("user").GetString() == "Actor"),
            e => Assert.Equal("A role, Z role", e.GetProperty("role").GetString()));
        var ordering = history.Select(e => (e.GetProperty("at").GetDateTimeOffset(), long.Parse(e.GetProperty("id").GetString()!))).ToArray();
        Assert.Equal(ordering.OrderBy(e => e.Item1).ThenBy(e => e.Item2), ordering);
        using var schemaResponse = await client.GetAsync("/openapi/v1.json", ct);
        Assert.Equal(HttpStatusCode.OK, schemaResponse.StatusCode);
        using var schema = JsonDocument.Parse(await schemaResponse.Content.ReadAsStringAsync(ct));
        var operation = schema.RootElement.GetProperty("paths").GetProperty("/api/messages/{id}/view").GetProperty("get");
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("403", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("404", out _));
        using var missing = await client.GetAsync("/api/messages/9223372036854775807/view", ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        foreach (var (user, status) in new[] { ("card-no-audit", HttpStatusCode.Forbidden),
                     ("card-wrong-audit", HttpStatusCode.NotFound), ("card-other-branch", HttpStatusCode.NotFound), ("card-other-department", HttpStatusCode.NotFound) })
        {
            client.DefaultRequestHeaders.Remove("X-Debug-User");
            client.DefaultRequestHeaders.Add("X-Debug-User", user);
            using var denied = await client.GetAsync($"/api/messages/{id}/view", ct);
            Assert.Equal(status, denied.StatusCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolvesUserMentionedOnlyAsPreviousAssignee(bool reassign)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File();
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        long id;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var admin = new User("assignment-auditor", "Administrator", true);
            var previous = new User("previous-assignee", "Previous reviewer");
            var next = new User("next-assignee", "Next reviewer");
            db.AddRange(admin, previous, next);
            await db.SaveChangesAsync(ct);
            id = await db.Messages.Select(m => m.Id).SingleAsync(ct);
            // The previous user is neither an audit actor nor a current assignee/reviewer,
            // so resolving this name requires loading PreviousAssigneeId explicitly.
            db.AuditEvents.Add(new AuditEvent(id,
                reassign ? AuditEventType.MessageReassigned : AuditEventType.MessageUnassigned,
                admin.Id, DateTimeOffset.UtcNow, null, null,
                JsonSerializer.Serialize(new { previousAssigneeId = previous.Id, assigneeId = reassign ? (int?)next.Id : null }),
                "assignment-history-test"));
            await db.SaveChangesAsync(ct);
        }
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "assignment-auditor");
        using var response = await client.GetAsync($"/api/messages/{id}/view", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var entry = Assert.Single(json.RootElement.GetProperty("auditLog").EnumerateArray(),
            e => e.GetProperty("type").GetString() == (reassign ? "reassign" : "unassign"));
        Assert.Equal(reassign ? "Previously assigned to Previous reviewer; Assigned to Next reviewer"
            : "Unassigned from Previous reviewer", entry.GetProperty("detail").GetString());
    }

    private sealed class Factory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }
}
