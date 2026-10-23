using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class AllE2ECleanupSqlTests
{
    [Theory]
    [InlineData("delete")]
    [InlineData("preview")]
    [InlineData("external-link")]
    [InlineData("conflicting-mode")]
    public async Task CleansOrphanFixturesAndPreservesPreexistingData(string mode)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File(name: "ordinary.prt");
        await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        await using var db = new ORPDbContext(fixture.Options);
        // Similar prefixes do not establish E2E ownership.
        var existingUser = new User("e2e.first.existing", "Existing user");
        var existingRole = new Role("E2E-existing");
        var existingWorkflow = new WorkflowDefinition("E2E_existing", "Existing workflow").AddStep(1, 1);
        db.AddRange(existingUser, existingRole, existingWorkflow);
        await db.SaveChangesAsync(ct);
        var baseline = await CountsAsync(db, ct);
        var firstRole = await AddRunAsync(db, true, ct);
        await AddRunAsync(db, false, ct); // No source/message remains for this run.
        db.Users.Add(new User("e2e.second." + Guid.NewGuid().ToString("N"), "Orphan reviewer"));
        db.WorkflowDefinitions.Add(new WorkflowDefinition("E2E_" + Guid.NewGuid().ToString("N"), "Orphan workflow").AddStep(1, 1));
        db.IgnoredImportFiles.Add(new IgnoredImportFile { FilePath = "approval-" + Guid.NewGuid().ToString("N") + ".prt",
            ContentHash = new string('c', 64), IgnoredAtUtc = DateTimeOffset.UtcNow, Reason = "NoDepartmentRule" });
        await db.SaveChangesAsync(ct);
        if (mode == "external-link")
        {
            db.UserRoles.Add(new UserRole { UserId = existingUser.Id, RoleId = firstRole,
                BranchId = await db.Branches.Where(b => b.Name == "Paris").Select(b => b.Id).SingleAsync(ct),
                DepartmentId = await db.Departments.Where(d => d.Name == "CS").Select(d => d.Id).SingleAsync(ct) });
            await db.SaveChangesAsync(ct);
        }
        var seeded = await CountsAsync(db, ct);
        var script = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "delete-messages-by-id-range.sql"), ct))
            .Replace("DECLARE @AllE2E bit = 0;", "DECLARE @AllE2E bit = 1;")
            .Replace("DECLARE @DryRun bit = 1;", $"DECLARE @DryRun bit = {(mode == "preview" ? 1 : 0)};");
        if (mode == "conflicting-mode")
            script = script.Replace("DECLARE @FromId bigint = NULL;", "DECLARE @FromId bigint = 1;");
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(script, connection);
        if (mode is "external-link" or "conflicting-mode")
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(ct));
            Assert.Equal(mode == "external-link" ? 51002 : 51000, error.Number);
        }
        else
            await command.ExecuteNonQueryAsync(ct);
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(mode == "delete" ? baseline : seeded, await CountsAsync(verify, ct));
        Assert.True(await verify.Users.AnyAsync(u => u.Id == existingUser.Id && u.DisplayName == "Existing user", ct));
        Assert.True(await verify.Roles.AnyAsync(r => r.Id == existingRole.Id && r.Name == "E2E-existing", ct));
        Assert.True(await verify.WorkflowDefinitions.AnyAsync(w => w.Id == existingWorkflow.Id && w.Code == "E2E_existing", ct));
        Assert.True(await verify.SwiftMessages.AnyAsync(s => s.FilePath == "ordinary.prt" && s.ContentHash == file.ContentHash, ct));
        if (mode == "external-link")
            Assert.True(await verify.UserRoles.AnyAsync(r => r.UserId == existingUser.Id && r.RoleId == firstRole, ct));
        if (mode == "delete")
        {
            // Repeating a global cleanup with nothing left is a no-op.
            await command.ExecuteNonQueryAsync(ct);
            Assert.Equal(baseline, await CountsAsync(verify, ct));
        }
    }

    private static async Task<int> AddRunAsync(ORPDbContext db, bool withMessage, CancellationToken ct)
    {
        var run = Guid.NewGuid().ToString("N");
        var branch = new Branch("E2E-" + run);
        var department = new Department("E2E-" + run);
        var role = new Role("E2E-" + run);
        var first = new User("e2e.first." + run, "First reviewer");
        var second = new User("e2e.second." + run, "Second reviewer");
        var workflow = new WorkflowDefinition("E2E_" + run, "E2E workflow").AddStep(1, 1);
        role.Permissions.Add(new RolePermission { Permission = await db.Permissions.FirstAsync(ct) });
        db.AddRange(branch, department, role, first, second, workflow);
        await db.SaveChangesAsync(ct);
        foreach (var user in new[] { first, second })
            db.UserRoles.Add(new UserRole { UserId = user.Id, BranchId = branch.Id, DepartmentId = department.Id, RoleId = role.Id });
        db.DepartmentRoutingRules.Add(new DepartmentRoutingRule { BranchId = branch.Id, DepartmentId = department.Id,
            WorkflowDefinitionId = workflow.Id, Direction = 1, MatchKind = 1, MessageTypePattern = "MT199", IsActive = true,
            SourceFile = "e2e-" + run, SourceSheet = "rules", SourceRow = 1 });
        var path = "approval-" + run + ".prt";
        db.IgnoredImportFiles.Add(new IgnoredImportFile { FilePath = path, ContentHash = new string('a', 64),
            IgnoredAtUtc = DateTimeOffset.UtcNow, Reason = "NoDepartmentRule" });
        await db.SaveChangesAsync(ct);
        if (withMessage)
        {
            var source = new SwiftMessageRecord { FileName = path, FilePath = path, ReferenceId = "E2E-" + run,
                Body = "Test", ContentHash = new string('a', 64) };
            db.SwiftMessages.Add(source);
            await db.SaveChangesAsync(ct);
            db.Messages.Add(new Message(source.MessageId, workflow.Id, branch.Id, department.Id, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
            var review = new Review(source.MessageId, 1, first.Id, DateTimeOffset.UtcNow);
            db.Reviews.Add(review);
            db.Assignments.Add(new Assignment(source.MessageId, first.Id, first.Id, DateTimeOffset.UtcNow));
            db.AuditEvents.Add(new AuditEvent(source.MessageId, AuditEventType.ReviewStarted, first.Id,
                DateTimeOffset.UtcNow, null, null, "{}", "all-e2e-test", review));
            await db.SaveChangesAsync(ct);
        }
        return role.Id;
    }

    private static async Task<int[]> CountsAsync(ORPDbContext db, CancellationToken ct) =>
    [
        await db.Messages.CountAsync(ct), await db.SwiftMessages.CountAsync(ct),
        await db.Reviews.CountAsync(ct), await db.Assignments.CountAsync(ct), await db.AuditEvents.CountAsync(ct),
        await db.MessageDepartments.CountAsync(ct), await db.IgnoredImportFiles.CountAsync(ct),
        await db.Users.CountAsync(ct), await db.Roles.CountAsync(ct), await db.UserRoles.CountAsync(ct),
        await db.RolePermissions.CountAsync(ct), await db.Permissions.CountAsync(ct),
        await db.Branches.CountAsync(ct), await db.Departments.CountAsync(ct),
        await db.WorkflowDefinitions.CountAsync(ct), await db.WorkflowSteps.CountAsync(ct),
        await db.DepartmentRoutingRules.CountAsync(ct)
    ];
}
