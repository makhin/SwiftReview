using Microsoft.EntityFrameworkCore;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class SeedTestDataTests
{
    [Fact]
    public async Task SeedCreatesMessagesAndDepartmentLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-test-data.sql"), ct);
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = script;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(ct);
        }
        Assert.Equal(100, await db.Messages.CountAsync(ct));
        Assert.Equal(100, await db.MessageDepartments.CountAsync(ct));
        Assert.Equal(4, await db.DepartmentRoutingRules.CountAsync(ct));
        Assert.Equal(100, await db.AuditEvents.CountAsync(ct));
        Assert.Equal("London", (await db.Branches.SingleAsync(ct)).Name);
        Assert.Equal(new[] { "CS", "DC", "TFO" }, await db.Departments.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync(ct));
        Assert.Equal(7, await db.Users.CountAsync(ct));
        Assert.Equal("admin", (await db.Users.SingleAsync(x => x.IsGlobalAdministrator, ct)).UserName);
        Assert.Equal(9, await db.UserRoles.CountAsync(ct));
        foreach (var department in new[] { "CS", "TFO", "DC" })
        {
            foreach (var suffix in new[] { 1, 2 })
            {
                var name = department.ToLowerInvariant() + ".reviewer" + suffix;
                var user = await db.Users.SingleAsync(x => x.UserName == name, ct);
                Assert.False(user.IsGlobalAdministrator);
                var scope = await db.UserRoles.SingleAsync(x => x.UserId == user.Id, ct);
                Assert.Equal(department, (await db.Departments.SingleAsync(x => x.Id == scope.DepartmentId, ct)).Name);
                Assert.Equal("London", (await db.Branches.SingleAsync(x => x.Id == scope.BranchId, ct)).Name);
                Assert.Equal("Reviewer", (await db.Roles.SingleAsync(x => x.Id == scope.RoleId, ct)).Name);
                var permissions = await (from rp in db.RolePermissions
                    join permission in db.Permissions on rp.PermissionId equals permission.Id
                    where rp.RoleId == scope.RoleId orderby permission.Name select permission.Name).ToArrayAsync(ct);
                Assert.Equal(new[] { "audit.view", "message.assign", "message.view", "review.level1",
                    "review.level2", "review.level3", "review.undo" }, permissions);
            }
        }
        var messages = await db.Messages.Include(x => x.Departments).ToListAsync(ct);
        Assert.All(messages, m =>
        {
            Assert.Equal(ORP.Domain.Messages.MessageState.New, m.State);
            Assert.Null(m.CurrentAssigneeId);
            Assert.True(Assert.Single(m.Departments).CanProcess);
        });
        Assert.All(await db.SwiftMessages.ToListAsync(ct), m => Assert.Equal("London", m.Branch));
        var routes = await db.SwiftMessages.GroupBy(x => new { x.MessageType, x.Direction })
            .Select(g => new { g.Key.MessageType, g.Key.Direction, Count = g.Count() }).ToListAsync(ct);
        Assert.Equal(4, routes.Count);
        Assert.All(routes, r => Assert.Equal(25, r.Count));
        Assert.Empty(await db.Reviews.ToListAsync(ct));
        Assert.Empty(await db.Assignments.ToListAsync(ct));
        var workflows = await db.WorkflowDefinitions.Include(x => x.Steps).ToListAsync(ct);
        foreach (var workflow in workflows.Where(x => x.ConfirmationMode != ORP.Domain.Workflows.ConfirmationMode.None))
        {
            Assert.Equal(ORP.Domain.Workflows.ConfirmationMode.Immediate, workflow.ConfirmationMode);
            Assert.True(workflow.AllowReviewerReuse);
            Assert.False(workflow.RejectAllowed);
            Assert.Equal(workflow.Code == "SINGLE_CONFIRMATION" ? 1 : 2, workflow.RequiredLevels().Count);
        }
        var pdf = Assert.Single(workflows, x => x.Code == "VIEW_ONLY");
        Assert.True(pdf.IsActive);
        Assert.Empty(pdf.RequiredLevels());
        var clear = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "clear-imported-messages.sql"), ct);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = clear;
            await command.ExecuteNonQueryAsync(ct);
        }
        Assert.Empty(await db.Messages.ToListAsync(ct));
        Assert.Empty(await db.MessageDepartments.ToListAsync(ct));
        Assert.Empty(await db.SwiftMessages.ToListAsync(ct));
        Assert.Empty(await db.AuditEvents.ToListAsync(ct));
        Assert.Equal(3, await db.WorkflowDefinitions.CountAsync(ct));
        Assert.Equal(4, await db.DepartmentRoutingRules.CountAsync(ct));
        Assert.Equal(7, await db.Users.CountAsync(ct));
    }
}
