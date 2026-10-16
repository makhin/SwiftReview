using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Importer;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class SqlSaaImportStoreTests
{
    [Fact]
    public async Task SeededMxRulesMatchSaaHeadersAndLegacyUpgradePreservesRuleSettings()
    {
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        db.Departments.AddRange(new Department("TFO"), new Department("DC"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var seed = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-department-routing-rules.sql"), TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(seed, TestContext.Current.CancellationToken);
        var seeded = await db.DepartmentRoutingRules.Where(rule => rule.SourceFile != "test").ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(315, seeded.Count);
        var mxRules = seeded.Where(rule => rule.MessageTypePattern.Contains('.')).ToArray();
        Assert.Equal(26, mxRules.Length);
        Assert.All(mxRules, rule => Assert.StartsWith("MX", rule.MessageTypePattern));
        var versioned = Assert.Single(mxRules, rule => rule.MessageTypePattern == "MXcamt.055.001.08");
        var id = versioned.Id;
        versioned.MessageTypePattern = "camt.055.001.08";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var upgrade = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fix-department-routing-mx-prefix.sql"), TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(upgrade, TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(upgrade, TestContext.Current.CancellationToken);
        await db.Entry(versioned).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(id, versioned.Id);
        Assert.True(versioned.IsActive);
        Assert.Equal("MXcamt.055.001.08", versioned.MessageTypePattern);
        Assert.Equal(316, await db.DepartmentRoutingRules.CountAsync(TestContext.Current.CancellationToken));
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(TestContext.Current.CancellationToken),
            await catalog.LoadBranchResolverAsync(TestContext.Current.CancellationToken));
        var file = fixture.File(type: "MXcamt.055.001.08");
        var result = processor.Process(new SaaMessageParser().Parse(file));
        Assert.Equal(SaaRoutingStatus.Matched, result.Status);
        Assert.Contains(versioned.DepartmentId, result.DepartmentIds);
    }

    [Fact]
    public async Task CatalogRejectsAnInvalidActiveMxPatternLoadedFromSql()
    {
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var rule = await db.DepartmentRoutingRules.SingleAsync(TestContext.Current.CancellationToken);
        rule.MatchKind = (int)SaaDepartmentMatchKind.MxFamily;
        rule.MessageTypePattern = ".";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SaaRoutingCatalog(fixture.ConnectionString)
            .LoadDepartmentResolverAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PersistsIgnoredHashesAcrossInstancesAndUpdatesOneRow()
    {
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var first = fixture.File();
        Assert.Equal(SaaFileImportResult.Ignored, await fixture.Store.IgnoreAsync(first, TestContext.Current.CancellationToken));
        Assert.Equal(SaaFileImportResult.AlreadyIgnored, await fixture.Store.IgnoreAsync(first, TestContext.Current.CancellationToken));
        Assert.Equal(SaaFileImportResult.AlreadyIgnored, await fixture.Store.FindAsync(first.RelativePath, first.ContentHash, false, TestContext.Current.CancellationToken));
        Assert.Null(await fixture.Store.FindAsync(first.RelativePath, first.ContentHash, true, TestContext.Current.CancellationToken));
        var second = fixture.File("changed");
        Assert.Null(await fixture.Store.FindAsync(second.RelativePath, second.ContentHash, false, TestContext.Current.CancellationToken));
        Assert.Equal(SaaFileImportResult.Ignored, await fixture.Store.IgnoreAsync(second, TestContext.Current.CancellationToken));
        await using var db = new ORPDbContext(fixture.Options);
        var ignored = Assert.Single(await db.IgnoredImportFiles.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(second.ContentHash, ignored.ContentHash);
        Assert.Equal("NoDepartmentRule", ignored.Reason);
        Assert.NotEqual(default, ignored.IgnoredAtUtc);
        Assert.Empty(await db.SwiftMessages.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RegistrationRemovesIgnoredMarkAndTakesPrecedenceOverLaterIgnores()
    {
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File();
        await fixture.Store.IgnoreAsync(file, TestContext.Current.CancellationToken);
        Assert.Equal(SaaFileImportResult.Imported, await fixture.Store.RegisterAsync(file, fixture.Route(file), TestContext.Current.CancellationToken));
        Assert.Equal(SaaFileImportResult.AlreadyImported, await fixture.Store.FindAsync(file.RelativePath, file.ContentHash, false, TestContext.Current.CancellationToken));
        Assert.Equal(SaaFileImportResult.AlreadyImported, await fixture.Store.IgnoreAsync(file, TestContext.Current.CancellationToken));
        var changed = fixture.File("changed");
        Assert.Equal(SaaFileImportResult.Conflict, await fixture.Store.IgnoreAsync(changed, TestContext.Current.CancellationToken));
        Assert.Equal(SaaFileImportResult.Conflict, await fixture.Store.FindAsync(changed.RelativePath, changed.ContentHash, true, TestContext.Current.CancellationToken));
        await AssertRegisteredExactlyOnce(fixture);
    }

    [Fact]
    public async Task MissingWorkflowRollsBackRegistrationAndRetainsIgnoredMark()
    {
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var file = fixture.File();
        await fixture.Store.IgnoreAsync(file, TestContext.Current.CancellationToken);
        await using var db = new ORPDbContext(fixture.Options);
        await db.WorkflowSteps.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await db.WorkflowDepartments.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await db.WorkflowDefinitions.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<FormatException>(() => fixture.Store.RegisterAsync(file, fixture.Route(file), TestContext.Current.CancellationToken));
        Assert.Single(await db.IgnoredImportFiles.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.SwiftMessages.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Messages.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("ignore", false)]
    [InlineData("register", false)]
    [InlineData("mixed", false)]
    [InlineData("register", true)]
    [InlineData("register", false, true)]
    public async Task SerializesOverlappingWritersAndVerifiesPersistedState(string kind, bool differentCase, bool joint = false)
    {
        await using var fixture = await SqlImportDatabase.CreateAsync();
        SaaMessageProcessor processor = fixture.Processor;
        if (joint)
        {
            await using var setup = new ORPDbContext(fixture.Options);
            var cs = await setup.Departments.SingleAsync(TestContext.Current.CancellationToken);
            var dc = new Department("DC");
            setup.Departments.Add(dc);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
            setup.DepartmentRoutingRules.Add(new DepartmentRoutingRule { DepartmentId = dc.Id, Direction = 1,
                MatchKind = 1, MessageTypePattern = "MT199", IsActive = true,
                SourceFile = "overlap", SourceSheet = "rules", SourceRow = 1 });
            setup.WorkflowDefinitions.Add(new ORP.Domain.Workflows.WorkflowDefinition(MessageDirection.Incoming,
                "Joint", "MT199", [cs.Id, dc.Id]).AddStep(1, 1));
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
            var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
            processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(TestContext.Current.CancellationToken),
                await catalog.LoadBranchResolverAsync(TestContext.Current.CancellationToken));
        }
        var firstFile = fixture.File();
        var secondFile = differentCase ? fixture.File(name: "SAMPLE.prt") : firstFile;
        SaaProcessedMessage Route(SaaSourceFileSnapshot file) => joint
            ? processor.Process(new SaaMessageParser().Parse(file)) : fixture.Route(file);
        // Hold the same application lock until both independent writers are confirmed waiting in SQL Server.
        await using var gate = new SqlConnection(fixture.ConnectionString);
        await gate.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = (SqlTransaction)await gate.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(@"
DECLARE @resource nvarchar(255) = N'orp-saa-file:' + CONVERT(nvarchar(20), CHECKSUM(@path COLLATE Latin1_General_100_CI_AS));
DECLARE @result int;
EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=0;
IF @result < 0 THROW 51000, 'Cannot acquire test gate', 1;", gate, transaction);
        command.Parameters.AddWithValue("@path", firstFile.RelativePath);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var first = kind == "register"
            ? fixture.Store.RegisterAsync(firstFile, Route(firstFile), TestContext.Current.CancellationToken) : fixture.Store.IgnoreAsync(firstFile, TestContext.Current.CancellationToken);
        var second = kind == "ignore"
            ? fixture.Store.IgnoreAsync(secondFile, TestContext.Current.CancellationToken) : fixture.Store.RegisterAsync(secondFile, Route(secondFile), TestContext.Current.CancellationToken);
        try
        {
            await WaitForTwoSqlWaiters(fixture.ConnectionString);
        }
        finally
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        var results = await Task.WhenAll(first, second);
        if (kind == "ignore")
        {
            Assert.Contains(SaaFileImportResult.Ignored, results);
            Assert.Contains(SaaFileImportResult.AlreadyIgnored, results);
            await using var db = new ORPDbContext(fixture.Options);
            Assert.Single(await db.IgnoredImportFiles.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.SwiftMessages.ToListAsync(TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.Equal(1, results.Count(result => result == SaaFileImportResult.Imported));
            if (kind == "register") Assert.Contains(SaaFileImportResult.AlreadyImported, results);
            await AssertRegisteredExactlyOnce(fixture, joint ? 2 : 1);
        }
    }

    private static async Task WaitForTwoSqlWaiters(string connectionString)
    {
        await using var observer = new SqlConnection(connectionString);
        await observer.OpenAsync(TestContext.Current.CancellationToken);
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(8))
        {
            await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_type='APPLICATION' AND request_status='WAIT' AND resource_database_id=DB_ID()", observer);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)) >= 2) return;
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
        Assert.Fail("Both writers must overlap while waiting for the held SQL application lock.");
    }

    private static async Task AssertRegisteredExactlyOnce(SqlImportDatabase fixture, int departmentCount = 1)
    {
        await using var db = new ORPDbContext(fixture.Options);
        Assert.Single(await db.SwiftMessages.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Messages.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(departmentCount, await db.MessageDepartments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.AuditEvents.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.IgnoredImportFiles.ToListAsync(TestContext.Current.CancellationToken));
    }
}
