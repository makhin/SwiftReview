using Microsoft.EntityFrameworkCore;
using FluentValidation;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using DevExtreme.AspNet.Data;
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
        var workflow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), "CS + DC").AddStep(1, 1);
        db.WorkflowDefinitions.Add(workflow);
        var view = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageView, ct);
        var assign = await db.Permissions.SingleAsync(p => p.Name == Permissions.MessageAssign, ct);
        var audit = await db.Permissions.SingleAsync(p => p.Name == Permissions.AuditView, ct);
        var role = new Role("Scoped reviewer");
        var csUser = new User("cs", "CS reviewer");
        var dcUser = new User("dc", "DC reviewer");
        var otherUser = new User("other", "Other branch reviewer");
        var splitUser = new User("split", "Split permissions reviewer");
        var sameScopeUser = new User("same-scope", "Combined roles reviewer");
        var admin = new User("admin", "Administrator", true);
        var viewRole = new Role("View only");
        var assignRole = new Role("Assign only");
        db.AddRange(role, viewRole, assignRole, csUser, dcUser, otherUser, splitUser, sameScopeUser, admin);
        await db.SaveChangesAsync(ct);
        db.RolePermissions.AddRange(new RolePermission { RoleId = role.Id, PermissionId = view.Id },
            new RolePermission { RoleId = role.Id, PermissionId = assign.Id },
            new RolePermission { RoleId = role.Id, PermissionId = audit.Id },
            new RolePermission { RoleId = viewRole.Id, PermissionId = view.Id },
            new RolePermission { RoleId = assignRole.Id, PermissionId = assign.Id });
        db.UserRoles.AddRange(
            new UserRole { UserId = csUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = role.Id },
            new UserRole { UserId = csUser.Id, BranchId = otherBranch.Id, DepartmentId = dc.Id, RoleId = viewRole.Id },
            new UserRole { UserId = dcUser.Id, BranchId = branch.Id, DepartmentId = dc.Id, RoleId = role.Id },
            new UserRole { UserId = otherUser.Id, BranchId = otherBranch.Id, DepartmentId = cs.Id, RoleId = role.Id },
            new UserRole { UserId = splitUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = viewRole.Id },
            new UserRole { UserId = splitUser.Id, BranchId = branch.Id, DepartmentId = dc.Id, RoleId = assignRole.Id },
            new UserRole { UserId = sameScopeUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = viewRole.Id },
            new UserRole { UserId = sameScopeUser.Id, BranchId = branch.Id, DepartmentId = cs.Id, RoleId = assignRole.Id });
        await db.SaveChangesAsync(ct);

        await db.DepartmentRoutingRules.ExecuteUpdateAsync(set => set.SetProperty(x => x.WorkflowDefinitionId, workflow.Id), ct);
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
        Assert.Equal(workflow.Id, (await new WorkflowResolver(db).ResolveAsync("MT199",
            MessageDirection.Incoming, [cs.Id, dc.Id], branch.Id, ct)).Workflow.Id);

        foreach (var user in new[] { csUser, dcUser })
        {
            var access = (await new UserAccessService(db).GetByIdAsync(user.Id, ct))!;
            var visibleDepartmentId = user.Id == csUser.Id ? cs.Id : dc.Id;
            var hiddenDepartmentId = user.Id == csUser.Id ? dc.Id : cs.Id;
            var visibleDepartmentName = user.Id == csUser.Id ? cs.Name : dc.Name;
            var grid = await new MessageGridQueries(db).LoadAsync(new() { Take = 20, RequireTotalCount = true }, access, null, ct);
            Assert.Equal(1, grid.totalCount);
            Assert.Equal([visibleDepartmentId], Assert.Single(grid.data.Cast<MessageGridRowDto>()).DepartmentIds);
            Assert.Equal(visibleDepartmentName, Assert.Single(grid.data.Cast<MessageGridRowDto>()).DepartmentNames);
            Assert.Equal(1, (await new MessageGridQueries(db).LoadAsync(
                new() { Take = 20, RequireTotalCount = true }, access, null, ct, [visibleDepartmentId])).totalCount);
            Assert.Equal(0, (await new MessageGridQueries(db).LoadAsync(
                new() { Take = 20, RequireTotalCount = true }, access, null, ct, [hiddenDepartmentId])).totalCount);
            Assert.Equal(1, (await new MessageGridQueries(db).StateCountsAsync(access, ct)).Total.Count);
            var search = await new MessageQueries(db).SearchAsync(new MessageSearchRequest(0, 20, null,
                new MessageFilter(null, null, null, [visibleDepartmentId], null, null)), access, ct);
            Assert.Equal(1, search.TotalCount);
            Assert.Equal([visibleDepartmentId], Assert.Single(search.Items).DepartmentIds);
            Assert.Equal(visibleDepartmentName, Assert.Single(search.Items).DepartmentNames);
            Assert.Equal(0, (await new MessageQueries(db).SearchAsync(new MessageSearchRequest(0, 20, null,
                new MessageFilter(null, null, null, [hiddenDepartmentId], null, null)), access, ct)).TotalCount);
            var details = await new MessageQueries(db).GetAsync(message.Id, access, ct);
            Assert.NotNull(details);
            Assert.Equal([visibleDepartmentId], details.DepartmentIds);
            Assert.Equal(visibleDepartmentName, details.DepartmentNames);
            var workflows = await new ReferenceDataQueries(db).GetWorkflowsAsync(access, ct);
            Assert.Equal(workflow.Code, Assert.Single(workflows, w => w.Id == workflow.Id).Code);
            var history = await new MessageQueries(db).AuditAsync(message.Id, new AuditTrailRequest(0, 20), access, ct);
            Assert.NotNull(history);
            Assert.Equal([visibleDepartmentId], Assert.Single(history.Items).Details.DepartmentIds);
            var rights = await new UserAuthorizationQueries(db).CheckAsync(user.Id, branch.Id, [cs.Id, dc.Id],
                Permissions.MessageAssign, ct);
            Assert.True(rights?.HasPermission);
        }
        var outsider = new UserAccess(otherUser.Id, otherUser.UserName, otherUser.DisplayName, false, []);
        Assert.Equal(0, (await new MessageGridQueries(db).LoadAsync(new() { Take = 20, RequireTotalCount = true }, outsider, null, ct)).totalCount);
        Assert.Null(await new MessageQueries(db).GetAsync(message.Id,
            (await new UserAccessService(db).GetByIdAsync(otherUser.Id, ct))!, ct));
        var adminAccess = (await new UserAccessService(db).GetByIdAsync(admin.Id, ct))!;
        Assert.Equal([cs.Id, dc.Id], Assert.Single((await new MessageGridQueries(db).LoadAsync(
            new() { Take = 20, RequireTotalCount = true }, adminAccess, null, ct)).data.Cast<MessageGridRowDto>()).DepartmentIds);
        Assert.Equal(workflow.Code, Assert.Single(await new ReferenceDataQueries(db)
            .GetWorkflowsAsync(adminAccess, ct), w => w.Id == workflow.Id).Code);
        Assert.Equal([cs.Id, dc.Id], Assert.Single((await new MessageQueries(db).AuditAsync(
            message.Id, new AuditTrailRequest(), adminAccess, ct))!.Items).Details.DepartmentIds);
        var splitAccess = (await new UserAccessService(db).GetByIdAsync(splitUser.Id, ct))!;
        Assert.Equal([cs.Id], (await new MessageQueries(db).GetAsync(message.Id, splitAccess, ct))!.DepartmentIds);
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
            new() { Take = 20, RequireTotalCount = true }, sameScopeAccess, MessageAssignmentScopes.Assignable, ct)).totalCount);
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
    public async Task JointWorkflowUsesConfiguredLevelsRegardlessOfDepartmentName()
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
        db.WorkflowDefinitions.Add(new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Invalid joint").AddStep(1, 1).AddStep(2, 2).AddStep(3, 3));
        await db.SaveChangesAsync(ct);
        var three = await db.WorkflowDefinitions.SingleAsync(x => x.Name == "Invalid joint", ct);
        await db.DepartmentRoutingRules.ExecuteUpdateAsync(set => set.SetProperty(x => x.WorkflowDefinitionId, three.Id), ct);
        var branch = await db.Branches.SingleAsync(ct);
        var resolved = await new WorkflowResolver(db).ResolveAsync(
            "MT199", MessageDirection.Incoming, [cs.Id, dc.Id], branch.Id, ct);
        Assert.Equal(new[] { 1, 2, 3 }, resolved.Workflow.RequiredLevels());
        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct),
            await catalog.LoadBranchResolverAsync(ct));
        var file = fixture.File();
        Assert.Equal(SaaFileImportResult.Imported, await fixture.Store.RegisterAsync(file,
            processor.Process(new SaaMessageParser().Parse(file)), ct));
        Assert.Single(await db.Messages.ToListAsync(ct));
    }
}
