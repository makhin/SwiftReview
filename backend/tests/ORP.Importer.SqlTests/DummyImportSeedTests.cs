using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Importer;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class DummyImportSeedTests
{
    private const string Source = "dummy-import-testing-2026-10-16";

    [Fact]
    public async Task SeedIsRepeatablePreservesApprovedRulesAndReusesExistingFamilyWorkflows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        db.Departments.AddRange(new Department("TFO"), new Department("DC"));
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlRawAsync(await Script("seed-department-routing-rules.sql"), ct);
        var department = await db.Departments.SingleAsync(d => d.Name == "CS", ct);
        var family = new WorkflowDefinition(MessageDirection.Incoming, "Existing family", "MXcamt.053",
            department.Id).AddStep(1, 1);
        db.WorkflowDefinitions.Add(family);
        await db.SaveChangesAsync(ct);
        var approvedIds = await db.DepartmentRoutingRules.Select(r => r.Id).OrderBy(id => id).ToArrayAsync(ct);

        var sql = await Script("seed-dummy-import-rules.sql");
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        var dummyIds = await db.DepartmentRoutingRules.Where(r => r.SourceFile == Source)
            .OrderBy(r => r.Id).Select(r => r.Id).ToArrayAsync(ct);
        var workflowIds = await db.WorkflowDefinitions.OrderBy(w => w.Id).Select(w => w.Id).ToArrayAsync(ct);
        Assert.Equal(16, dummyIds.Length);
        Assert.Equal(15, workflowIds.Length); // Fixture + existing family + 13 uncovered routes.
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        Assert.Equal(dummyIds, await db.DepartmentRoutingRules.Where(r => r.SourceFile == Source)
            .OrderBy(r => r.Id).Select(r => r.Id).ToArrayAsync(ct));
        Assert.Equal(approvedIds, await db.DepartmentRoutingRules.Where(r => r.SourceFile != Source)
            .OrderBy(r => r.Id).Select(r => r.Id).ToArrayAsync(ct));
        Assert.Equal(workflowIds, await db.WorkflowDefinitions.OrderBy(w => w.Id).Select(w => w.Id).ToArrayAsync(ct));

        var workflows = await db.WorkflowDefinitions.Include(w => w.Steps).ToListAsync(ct);
        Assert.All(workflows.Where(w => w.Name.StartsWith("DUMMY import - ")), w =>
            Assert.Equal(new[] { 1, 2 }, w.RequiredLevels()));
        Assert.Single(family.Steps);
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var departments = await catalog.LoadDepartmentResolverAsync(ct);
        var branches = await db.Branches.ToDictionaryAsync(b => b.Name, b => b.Id, ct);
        var rules = await db.DepartmentRoutingRules.Where(r => r.SourceFile == Source).ToListAsync(ct);
        foreach (var rule in rules)
        {
            Assert.Equal(department.Id, departments.Resolve(rule.BranchId!.Value, rule.MessageTypePattern,
                (SaaMessageDirection)rule.Direction)!.DepartmentIds.Single());
            var workflow = await new WorkflowResolver(db).ResolveAsync(rule.MessageTypePattern,
                (MessageDirection)rule.Direction, [department.Id], rule.BranchId.Value, ct);
            Assert.NotNull(workflow);
        }
        Assert.Equal(family.Id, (await new WorkflowResolver(db).ResolveAsync("MXcamt.053.001.14",
            MessageDirection.Incoming, [department.Id], branches["Brussels"], ct)).Id);
        Assert.NotNull(await new WorkflowResolver(db).ResolveAsync("MXcamt.055.001.08",
            MessageDirection.Incoming, [department.Id], branches["London"], ct));

        // An additional active rule adds its department instead of overriding CS.
        var tfo = await db.Departments.SingleAsync(d => d.Name == "TFO", ct);
        db.DepartmentRoutingRules.Add(new DepartmentRoutingRule
        {
            DepartmentId = tfo.Id, BranchId = branches["Dubai"], Direction = 1, MatchKind = 1,
            MessageTypePattern = "MT081", IsActive = true,
            SourceFile = "approved-test", SourceSheet = "test", SourceRow = 1
        });
        await db.SaveChangesAsync(ct);
        departments = await catalog.LoadDepartmentResolverAsync(ct);
        Assert.Contains(department.Id, departments.Resolve(branches["Dubai"], "MT081", SaaMessageDirection.Incoming)!.DepartmentIds);
        Assert.Contains(tfo.Id, departments.Resolve(branches["Dubai"], "MT081", SaaMessageDirection.Incoming)!.DepartmentIds);
    }

    [Theory]
    [InlineData("MXcamt.053.001.08", false)]
    [InlineData("MXcamt.053.001.08", true)]
    [InlineData("MXcamt.053", false)]
    [InlineData("MXcamt.053", true)]
    public async Task JointWorkflowDoesNotSuppressSingletonWorkflow(string pattern, bool branchSpecific)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        db.Departments.AddRange(new Department("TFO"), new Department("DC"));
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlRawAsync(await Script("seed-department-routing-rules.sql"), ct);
        var cs = await db.Departments.SingleAsync(d => d.Name == "CS", ct);
        var dc = await db.Departments.SingleAsync(d => d.Name == "DC", ct);
        var paris = await db.Branches.SingleAsync(b => b.Name == "Paris", ct);
        var joint = new WorkflowDefinition(MessageDirection.Incoming, "Existing joint workflow", pattern,
            new[] { cs.Id, dc.Id }, branchSpecific ? paris.Id : null).AddStep(1, 1);
        db.WorkflowDefinitions.Add(joint);
        await db.SaveChangesAsync(ct);

        var sql = await Script("seed-dummy-import-rules.sql");
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var departments = await catalog.LoadDepartmentResolverAsync(ct);
        var resolution = departments.Resolve(paris.Id, "MXcamt.053.001.08", SaaMessageDirection.Incoming)!;
        Assert.Equal(new[] { cs.Id }, resolution.DepartmentIds);
        var workflow = await new WorkflowResolver(db).ResolveAsync("MXcamt.053.001.08",
            MessageDirection.Incoming, resolution.DepartmentIds, paris.Id, ct);
        Assert.NotEqual(joint.Id, workflow.Id);
        Assert.Equal(new[] { cs.Id }, workflow.DepartmentIds);

        var workflowIds = await db.WorkflowDefinitions.OrderBy(w => w.Id).Select(w => w.Id).ToArrayAsync(ct);
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        Assert.Equal(workflowIds, await db.WorkflowDefinitions.OrderBy(w => w.Id).Select(w => w.Id).ToArrayAsync(ct));
        db.ChangeTracker.Clear();
        var preserved = await db.WorkflowDefinitions.Include(w => w.Departments).SingleAsync(w => w.Id == joint.Id, ct);
        Assert.Equal(new[] { cs.Id, dc.Id }.Order(), preserved.DepartmentIds);
    }

    [Theory]
    [InlineData("missingBranch")]
    [InlineData("ambiguousBranch")]
    [InlineData("missingDepartment")]
    [InlineData("ambiguousDepartment")]
    public async Task InvalidDirectoriesFailWithoutPartialSeeding(string problem)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        db.Branches.AddRange(new Branch("Dubai"), new Branch("Brussels"), new Branch("Milan"));
        if (problem != "missingBranch") db.Branches.Add(new Branch("London"));
        if (problem == "ambiguousBranch") db.Branches.Add(new Branch("Paris"));
        if (problem == "ambiguousDepartment") db.Departments.Add(new Department("CS"));
        await db.SaveChangesAsync(ct);
        if (problem == "missingDepartment")
            await db.Departments.ExecuteUpdateAsync(update => update.SetProperty(d => d.Name, "Other"), ct);
        var rulesBefore = await db.DepartmentRoutingRules.CountAsync(ct);
        var workflowsBefore = await db.WorkflowDefinitions.CountAsync(ct);
        var sql = await Script("seed-dummy-import-rules.sql");
        var error = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync(sql, ct));
        Assert.Contains("requires", error.Message);
        Assert.Equal(rulesBefore, await db.DepartmentRoutingRules.CountAsync(ct));
        Assert.Equal(workflowsBefore, await db.WorkflowDefinitions.CountAsync(ct));
    }

    private static Task<string> Script(string name) => File.ReadAllTextAsync(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name), TestContext.Current.CancellationToken);
}
