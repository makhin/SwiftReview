using Microsoft.EntityFrameworkCore;
using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Importer;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class MultiDepartmentSqlTests
{
    [Fact]
    public async Task SharedMessageAppearsOnceAndIsAccessibleFromEitherDepartment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var cs = await db.Departments.SingleAsync(ct);
        var branch = await db.Branches.SingleAsync(ct);
        var dc = new Department("DC");
        var otherBranch = new Branch("London");
        db.AddRange(dc, otherBranch);
        await db.SaveChangesAsync(ct);
        db.DepartmentRoutingRules.AddRange(
            new DepartmentRoutingRule { DepartmentId = cs.Id, Direction = 1, MatchKind = 1,
                MessageTypePattern = "MT199", IsActive = true, SourceFile = "overlap", SourceSheet = "rules", SourceRow = 1 },
            new DepartmentRoutingRule { DepartmentId = dc.Id, Direction = 1, MatchKind = 1,
                MessageTypePattern = "MT199", IsActive = true, SourceFile = "overlap", SourceSheet = "rules", SourceRow = 2 });
        var workflow = new WorkflowDefinition(MessageDirection.Incoming, "CS + DC", "MT199", [cs.Id, dc.Id]).AddStep(1, 1);
        db.WorkflowDefinitions.Add(workflow);
        var view = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageView, ct);
        var assign = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageAssign, ct);
        var role = new Role("Scoped reviewer");
        var csUser = new User("cs", "CS reviewer");
        var dcUser = new User("dc", "DC reviewer");
        var otherUser = new User("other", "Other branch reviewer");
        var splitUser = new User("split", "Split permissions reviewer");
        var sameScopeUser = new User("same-scope", "Combined roles reviewer");
        var viewRole = new Role("View only");
        var assignRole = new Role("Assign only");
        db.AddRange(role, viewRole, assignRole, csUser, dcUser, otherUser, splitUser, sameScopeUser);
        await db.SaveChangesAsync(ct);
        db.RolePermissions.AddRange(new RolePermission { RoleId = role.Id, PermissionId = view.Id },
            new RolePermission { RoleId = role.Id, PermissionId = assign.Id },
            new RolePermission { RoleId = viewRole.Id, PermissionId = view.Id },
            new RolePermission { RoleId = assignRole.Id, PermissionId = assign.Id });
        db.UserRoles.AddRange(
            new UserRole { UserId = csUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = role.Id },
            new UserRole { UserId = dcUser.Id, BranchId = branch.Id, DepartmentId = dc.Id, RoleId = role.Id },
            new UserRole { UserId = otherUser.Id, BranchId = otherBranch.Id, DepartmentId = cs.Id, RoleId = role.Id },
            new UserRole { UserId = splitUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = viewRole.Id },
            new UserRole { UserId = splitUser.Id, BranchId = branch.Id, DepartmentId = dc.Id, RoleId = assignRole.Id },
            new UserRole { UserId = sameScopeUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = viewRole.Id },
            new UserRole { UserId = sameScopeUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = assignRole.Id });
        await db.SaveChangesAsync(ct);

        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct),
            await catalog.LoadBranchResolverAsync(ct));
        var file = fixture.File();
        var routed = processor.Process(new SaaMessageParser().Parse(file));
        Assert.Equal([cs.Id, dc.Id], routed.DepartmentIds);
        Assert.Equal(SaaFileImportResult.Imported, await fixture.Store.RegisterAsync(file, routed, ct));
        Assert.Equal(SaaFileImportResult.AlreadyImported, await fixture.Store.RegisterAsync(file, routed, ct));
        var message = await db.Messages.Include(x => x.Departments).SingleAsync(ct);
        Assert.Equal([cs.Id, dc.Id], message.DepartmentIds);
        Assert.Equal(workflow.Id, message.WorkflowDefinitionId);
        Assert.Single(await db.SwiftMessages.ToListAsync(ct));
        Assert.Single(await db.AuditEvents.ToListAsync(ct));

        foreach (var user in new[] { csUser, dcUser })
        {
            var access = new UserAccess(user.Id, user.UserName, user.DisplayName, false,
                [new UserScopeAccess(branch.Id, user.Id == csUser.Id ? cs.Id : dc.Id, [role.Id],
                    [Permissions.MessageView, Permissions.MessageAssign])]);
            var grid = await new MessageGridQueries(db).LoadAsync(new MessageGridRequest(), access, ct);
            Assert.Equal(1, grid.TotalCount);
            Assert.Equal([cs.Id, dc.Id], Assert.Single(grid.Items).DepartmentIds);
            Assert.Equal(1, (await new MessageGridQueries(db).LoadAsync(
                new MessageGridRequest(Departments: [dc.Id]), access, ct)).TotalCount);
            Assert.Equal(1, (await new MessageGridQueries(db).StateCountsAsync(access, ct)).Total.Count);
            var search = await new MessageQueries(db).SearchAsync(new MessageSearchRequest(0, 20, null,
                new MessageFilter(null, null, null, [dc.Id], null, null)), access, ct);
            Assert.Equal(1, search.TotalCount);
            var rights = await new UserAuthorizationQueries(db).CheckAsync(user.Id, branch.Id, [cs.Id, dc.Id],
                Permissions.MessageAssign, ct);
            Assert.True(rights?.HasPermission);
        }
        var outsider = new UserAccess(otherUser.Id, otherUser.UserName, otherUser.DisplayName, false, []);
        Assert.Equal(0, (await new MessageGridQueries(db).LoadAsync(new MessageGridRequest(), outsider, ct)).TotalCount);
        var splitRights = await new UserAuthorizationQueries(db).CheckAsync(splitUser.Id, branch.Id, [cs.Id, dc.Id],
            Permissions.MessageAssign, ct);
        Assert.True(splitRights?.CanView);
        Assert.False(splitRights?.HasPermission);
        var sameScopeRights = await new UserAuthorizationQueries(db).CheckAsync(sameScopeUser.Id, branch.Id,
            [cs.Id, dc.Id], Permissions.MessageAssign, ct);
        Assert.True(sameScopeRights?.HasPermission);
        var sameScopeAccess = new UserAccess(sameScopeUser.Id, sameScopeUser.UserName, sameScopeUser.DisplayName,
            false, [new UserScopeAccess(branch.Id, cs.Id, [viewRole.Id, assignRole.Id],
                [Permissions.MessageView, Permissions.MessageAssign])]);
        Assert.Equal(1, (await new MessageGridQueries(db).LoadAsync(
            new MessageGridRequest(AssignmentScope: MessageAssignmentScopes.Assignable), sameScopeAccess, ct)).TotalCount);
    }

    [Fact]
    public async Task MissingJointWorkflowLeavesNoImportedMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var dc = new Department("DC");
        db.Departments.Add(dc);
        await db.SaveChangesAsync(ct);
        db.DepartmentRoutingRules.Add(new DepartmentRoutingRule { DepartmentId = dc.Id, Direction = 1,
            MatchKind = 1, MessageTypePattern = "MT199", IsActive = true,
            SourceFile = "overlap", SourceSheet = "rules", SourceRow = 1 });
        await db.SaveChangesAsync(ct);
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct),
            await catalog.LoadBranchResolverAsync(ct));
        var file = fixture.File();
        await Assert.ThrowsAsync<FormatException>(() => fixture.Store.RegisterAsync(file,
            processor.Process(new SaaMessageParser().Parse(file)), ct));
        Assert.Empty(await db.SwiftMessages.ToListAsync(ct));
        Assert.Empty(await db.Messages.ToListAsync(ct));
        Assert.Empty(await db.MessageDepartments.ToListAsync(ct));
        Assert.Empty(await db.AuditEvents.ToListAsync(ct));
    }

    [Fact]
    public async Task JointWorkflowIncludingCsRejectsThirdReviewLevel()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var cs = await db.Departments.SingleAsync(ct);
        var dc = new Department("DC");
        db.Departments.Add(dc);
        await db.SaveChangesAsync(ct);
        db.DepartmentRoutingRules.Add(new DepartmentRoutingRule { DepartmentId = dc.Id, Direction = 1,
            MatchKind = 1, MessageTypePattern = "MT199", IsActive = true,
            SourceFile = "overlap", SourceSheet = "rules", SourceRow = 1 });
        db.WorkflowDefinitions.Add(new WorkflowDefinition(MessageDirection.Incoming, "Invalid joint", "MT199",
            [cs.Id, dc.Id]).AddStep(1, 1).AddStep(2, 2).AddStep(3, 3));
        await db.SaveChangesAsync(ct);
        var branch = await db.Branches.SingleAsync(ct);
        await Assert.ThrowsAsync<ValidationException>(() => new WorkflowResolver(db).ResolveAsync(
            "MT199", MessageDirection.Incoming, [cs.Id, dc.Id], branch.Id, ct));
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct),
            await catalog.LoadBranchResolverAsync(ct));
        var file = fixture.File();
        var error = await Assert.ThrowsAsync<FormatException>(() => fixture.Store.RegisterAsync(file,
            processor.Process(new SaaMessageParser().Parse(file)), ct));
        Assert.Contains("CS and TFO", error.Message);
        Assert.Empty(await db.Messages.ToListAsync(ct));
    }
}
