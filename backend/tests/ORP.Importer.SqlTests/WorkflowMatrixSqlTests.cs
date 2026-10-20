using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Application.Grids;
using ORP.Domain.Common;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Importer;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Logging;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class WorkflowMatrixSqlTests
{
    [Fact]
    public async Task All315RulesSelectSharedPoliciesAndSeedIsRepeatable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        await Seed(db, ct);
        // The fixture's unrelated test configuration is preserved, but is outside the matrix.
        var count = await db.WorkflowDefinitions.CountAsync(ct);
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-department-routing-rules.sql"), ct);
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        Assert.Equal(count, await db.WorkflowDefinitions.CountAsync(ct));
        var rules = await db.DepartmentRoutingRules.Where(x => x.SourceFile.EndsWith(".xlsx")).ToListAsync(ct);
        Assert.Equal(315, rules.Count);
        var workflows = await db.WorkflowDefinitions.Include(x => x.Steps).ToDictionaryAsync(x => x.Id, ct);
        var counts = rules.GroupBy(x => workflows[x.WorkflowDefinitionId!.Value].Code).ToDictionary(x => x.Key, x => x.Count());
        Assert.Equal(49, counts["VIEW_ONLY"]);
        Assert.Equal(96, counts["SINGLE_CONFIRMATION"]);
        Assert.Equal(170, counts["DOUBLE_CONFIRMATION"]);
        Assert.Equal(3, counts.Count);
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var departments = await catalog.LoadDepartmentResolverAsync(ct);
        var defaultBranch = await db.Branches.Where(x => x.Name == "London").Select(x => x.Id).SingleAsync(ct);
        foreach (var rule in rules)
        {
            var type = rule.MatchKind == 2 ? rule.MessageTypePattern[..3] + "00" :
                rule.MatchKind == 3 ? rule.MessageTypePattern + ".001.01" : rule.MessageTypePattern;
            var branch = rule.BranchId ?? defaultBranch;
            var scope = departments.Resolve(branch, type, (SaaMessageDirection)rule.Direction)!;
            var resolved = await new WorkflowResolver(db).ResolveAsync(type, (MessageDirection)rule.Direction, scope.DepartmentIds, branch, ct);
            Assert.Contains(rule.DepartmentId, resolved.DepartmentIds);
            var policy = workflows[rule.WorkflowDefinitionId!.Value];
            Assert.Equal(policy.ConfirmationMode != ConfirmationMode.None, resolved.ProcessingDepartmentIds.Contains(rule.DepartmentId));
            if (policy.ConfirmationMode != ConfirmationMode.None) Assert.Equal(policy.Id, resolved.Workflow.Id);
        }
    }

    [Fact]
    public async Task ImportedScopesEnforceViewOnlyAndPreserveSnapshotAfterRuleChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        await Seed(db, ct);
        var branches = await db.Branches.ToDictionaryAsync(x => x.Name, x => x.Id, ct);
        var departments = await db.Departments.ToDictionaryAsync(x => x.Name, x => x.Id, ct);
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct), await catalog.LoadBranchResolverAsync(ct));
        var cases = new[]
        {
            ("London", "MT999", 1, "DOUBLE_CONFIRMATION", "CS"),
            ("London", "MXpacs.008.001.08", 1, "DOUBLE_CONFIRMATION", "CS"),
            ("Dubai", "MXpacs.008.001.08", 1, "DOUBLE_CONFIRMATION", "CS,TFO"),
            ("Dubai", "MT101", 1, "DOUBLE_CONFIRMATION", "TFO"),
            ("Frankfurt", "MT999", 1, "DOUBLE_CONFIRMATION", "CS,TFO"),
            ("Dubai", "MT700", 2, "SINGLE_CONFIRMATION", "TFO"),
            ("Paris", "MT671", 1, "SINGLE_CONFIRMATION", "DC"),
            ("Paris", "MT094", 1, "VIEW_ONLY", ""),
            ("Paris", "MT400", 1, "VIEW_ONLY", "")
        };
        foreach (var (branch, type, direction, code, processing) in cases)
        {
            var file = fixture.File(name: $"case-{branch}-{type}-{direction}.prt", type: type);
            var text = await File.ReadAllTextAsync(file.FilePath, ct);
            await File.WriteAllTextAsync(file.FilePath, text.Replace("Branch:Paris|", $"Branch:{branch}|")
                .Replace("DIRECTION:Incoming", direction == 1 ? "DIRECTION:Incoming" : "DIRECTION:Outgoing"), ct);
            file = new SaaSourceFileReader().Read(file.FilePath, fixture.Root);
            var routed = processor.Process(new SaaMessageParser().Parse(file));
            Assert.Equal(SaaFileImportResult.Imported, await fixture.Store.RegisterAsync(file, routed, ct));
            var message = await db.Messages.Include(x => x.Departments)
                .SingleAsync(x => x.Id == db.SwiftMessages.Where(s => s.FilePath == file.RelativePath).Select(s => s.MessageId).Single(), ct);
            Assert.Equal(code, (await db.WorkflowDefinitions.SingleAsync(x => x.Id == message.WorkflowDefinitionId, ct)).Code);
            Assert.Equal(processing.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => departments[x]).Order(), message.ProcessingDepartmentIds);
            Assert.Equal(code == "VIEW_ONLY" ? MessageState.ViewOnly : MessageState.New, message.State);
        }
        var role = new Role("Matrix all permissions");
        var cs = new User("matrix-cs", "CS");
        var tfo = new User("matrix-tfo", "TFO");
        var admin = new User("matrix-admin", "Admin", true);
        db.AddRange(role, cs, tfo, admin);
        await db.SaveChangesAsync(ct);
        foreach (var permission in await db.Permissions.ToListAsync(ct))
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        db.UserRoles.AddRange(
            new UserRole { UserId = cs.Id, BranchId = branches["London"], DepartmentId = departments["CS"], RoleId = role.Id },
            new UserRole { UserId = tfo.Id, BranchId = branches["London"], DepartmentId = departments["TFO"], RoleId = role.Id });
        await db.SaveChangesAsync(ct);
        var shared = await db.Messages.SingleAsync(x => x.BranchId == branches["London"] &&
            db.SwiftMessages.Any(s => s.MessageId == x.Id && s.MessageType == "MT999"), ct);
        var tfoAccess = (await new UserAccessService(db).GetByIdAsync(tfo.Id, ct))!;
        Assert.NotNull(await new MessageQueries(db).GetAsync(shared.Id, tfoAccess, ct));
        Assert.NotNull(await new MessageQueries(db).AuditAsync(shared.Id, new AuditTrailRequest(), tfoAccess, ct));
        Assert.Contains((await new MessageGridQueries(db).LoadAsync(new MessageGridRequest(), tfoAccess, ct)).Items,
            row => row.Id == shared.Id);
        foreach (var permission in new[] { Permissions.MessageAssign, Permissions.WorkflowManage, Permissions.ReviewLevel1,
            Permissions.ReviewLevel2, Permissions.ReviewLevel3, Permissions.ReviewUndo })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Authorization(db, tfo.Id).RequireAsync(shared.Id, permission, ct));
        await Authorization(db, cs.Id).RequireAsync(shared.Id, Permissions.MessageAssign, ct);
        // Rules change future imports only, never the processing scope of this message.
        var two = await db.WorkflowDefinitions.SingleAsync(x => x.Code == "DOUBLE_CONFIRMATION", ct);
        await db.DepartmentRoutingRules.Where(x => x.DepartmentId == departments["TFO"] && x.BranchId == branches["London"])
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.WorkflowDefinitionId, two.Id), ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Authorization(db, tfo.Id).RequireAsync(shared.Id, Permissions.ReviewLevel1, ct));
        var viewOnly = await db.Messages.FirstAsync(x => x.State == MessageState.ViewOnly, ct);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Authorization(db, admin.Id).RequireAsync(viewOnly.Id, Permissions.MessageAssign, ct));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Authorization(db, admin.Id).RequireAsync(viewOnly.Id, Permissions.ReviewLevel1, ct));
        foreach (var permission in new[] { Permissions.MessageAssign, Permissions.WorkflowManage, Permissions.ReviewLevel1 })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Authorization(db, tfo.Id).RequireAsync(viewOnly.Id, permission, ct));
        var adminAccess = (await new UserAccessService(db).GetByIdAsync(admin.Id, ct))!;
        var summary = await new MessageQueries(db).DashboardAsync(adminAccess, ct);
        Assert.Equal(9, summary.TotalMessages);
        Assert.Equal(2, summary.ViewOnly);
        Assert.Equal(0, (await new MessageQueries(db).DashboardAsync(tfoAccess, ct)).ViewOnly);
        Assert.Equal(7, summary.Unassigned);
        Assert.Equal(0, summary.Completed);
        var states = await new MessageGridQueries(db).StateCountsAsync(adminAccess, null, ct);
        Assert.Equal(2, states.Items.Single(x => x.State == MessageState.ViewOnly).Count);
        Assert.Empty(await db.Assignments.ToListAsync(ct));
        Assert.Empty(await db.Reviews.ToListAsync(ct));
        Assert.All(await db.AuditEvents.ToListAsync(ct), x => Assert.Equal(ORP.Domain.Auditing.AuditEventType.MessageRegistered, x.EventType));
    }

    [Fact]
    public async Task ConflictingDepartmentWorkflowsFailWithoutRegisteringAnyData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        await Seed(db, ct);
        var single = await db.WorkflowDefinitions.SingleAsync(x => x.Code == "SINGLE_CONFIRMATION", ct);
        var cs = await db.Departments.SingleAsync(x => x.Name == "CS", ct);
        await db.DepartmentRoutingRules.Where(x => x.DepartmentId == cs.Id && x.MessageTypePattern == "MT999")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.WorkflowDefinitionId, single.Id), ct);
        var file = fixture.File(type: "MT999");
        var text = await File.ReadAllTextAsync(file.FilePath, ct);
        await File.WriteAllTextAsync(file.FilePath, text.Replace("Branch:Paris|", "Branch:Frankfurt|"), ct);
        file = new SaaSourceFileReader().Read(file.FilePath, fixture.Root);
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct), await catalog.LoadBranchResolverAsync(ct));
        var routed = processor.Process(new SaaMessageParser().Parse(file));
        var error = await Assert.ThrowsAsync<FormatException>(() => fixture.Store.RegisterAsync(file, routed, ct));
        Assert.Contains("incompatible workflows", error.Message);
        Assert.Empty(await db.SwiftMessages.ToListAsync(ct));
        Assert.Empty(await db.Messages.ToListAsync(ct));
        Assert.Empty(await db.AuditEvents.ToListAsync(ct));
    }

    private static async Task Seed(ORPDbContext db, CancellationToken ct)
    {
        await db.DepartmentRoutingRules.Where(x => x.SourceFile == "test").ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        db.Departments.AddRange(new Department("TFO"), new Department("DC"));
        await db.SaveChangesAsync(ct);
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-department-routing-rules.sql"), ct);
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
    private static MessageAuthorizationService Authorization(ORPDbContext db, int user) => new(
        new ORPStore(db, new BusinessActionLog(NullLogger<ORPStore>.Instance)), new UserAuthorizationQueries(db),
        new Actor(user), new Correlation(), NullLogger<MessageAuthorizationService>.Instance);
    private sealed record Actor(int UserId) : ICurrentUser
    {
        public string UserName => "matrix";
        public string DisplayName => "Matrix";
    }
    private sealed class Correlation : ICorrelationContext { public string CorrelationId => "matrix"; }
}
