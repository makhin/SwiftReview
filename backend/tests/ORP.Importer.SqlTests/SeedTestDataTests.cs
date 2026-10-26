using Microsoft.EntityFrameworkCore;
using ORP.Domain.DistributionLists;
using ORP.Domain.Identity;
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
        await SeedAsync(db, ct);
        var connection = db.Database.GetDbConnection();
        Assert.Equal(100, await db.Messages.CountAsync(ct));
        Assert.Equal(100, await db.MessageDepartments.CountAsync(ct));
        Assert.Equal(4, await db.DepartmentRoutingRules.CountAsync(ct));
        Assert.Equal(100, await db.AuditEvents.CountAsync(ct));
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
        Assert.Equal(5, await db.Users.CountAsync(ct));
    }

    [Fact]
    public async Task SeedRestoresCompletePermissionCatalogAndReviewerDistributionListGrants()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        await SeedAsync(db, ct);

        var permissions = await db.Permissions.Select(x => x.Name).ToArrayAsync(ct);
        Assert.Equal(Permissions.All.Order(), permissions.Order());
        var reviewer = await db.Roles.Include(x => x.Permissions).ThenInclude(x => x.Permission)
            .SingleAsync(x => x.Name == "Reviewer", ct);
        Assert.Contains(reviewer.Permissions, x => x.Permission.Name == Permissions.MessageForward);
        Assert.Contains(reviewer.Permissions, x => x.Permission.Name == Permissions.DistributionListManage);
    }

    [Fact]
    public async Task SeedCanResetExistingDistributionListsAndTheirEmails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        await SeedAsync(db, ct);
        var migrations = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        var departmentId = await db.Departments.Select(x => x.Id).FirstAsync(ct);
        db.DistributionLists.Add(new DistributionList(departmentId, "Before reset", ["one@example.com", "two@example.com"]));
        await db.SaveChangesAsync(ct);
        Assert.Equal(2, await db.DistributionListEmails.CountAsync(ct));
        db.ChangeTracker.Clear();

        await SeedAsync(db, ct);

        Assert.Empty(await db.DistributionLists.ToListAsync(ct));
        Assert.Empty(await db.DistributionListEmails.ToListAsync(ct));
        Assert.Equal(100, await db.Messages.CountAsync(ct));
        Assert.Equal(3, await db.Departments.CountAsync(ct));
        Assert.False(await db.Departments.AnyAsync(x => x.Id == departmentId, ct));
        Assert.Equal(migrations, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal(Permissions.All.Order(), (await db.Permissions.Select(x => x.Name).ToArrayAsync(ct)).Order());
    }

    private static async Task SeedAsync(ORPDbContext db, CancellationToken ct)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-test-data.sql"), ct);
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = script;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(ct);
    }
}
