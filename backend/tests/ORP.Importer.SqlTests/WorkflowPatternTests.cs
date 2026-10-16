using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Importer;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class WorkflowPatternTests
{
    [Theory]
    [InlineData("MT4xx", "mt499", false, false)]
    [InlineData("MXpacs.008", "MXpacs.008.001.08", false, false)]
    [InlineData("MT4xx", "MT450", true, false)]
    [InlineData("MXpacs.008", "MXpacs.008.001.08", true, false)]
    [InlineData("MT4xx", "MT450", true, true)]
    public async Task ImportAndApplicationResolverUseTheSamePatternAndScopeSelection(
        string pattern, string type, bool addExact, bool scopedPattern)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var branch = await db.Branches.SingleAsync(ct);
        var department = await db.Departments.SingleAsync(ct);
        var rule = await db.DepartmentRoutingRules.SingleAsync(ct);
        rule.MessageTypePattern = type;
        var general = new WorkflowDefinition(MessageDirection.Incoming, "Pattern", pattern,
            department.Id, scopedPattern ? branch.Id : null).AddStep(1, 1);
        db.WorkflowDefinitions.Add(general);
        var expected = general;
        if (addExact)
        {
            var exact = new WorkflowDefinition(MessageDirection.Incoming, "Exact", type, department.Id).AddStep(1, 1);
            db.WorkflowDefinitions.Add(exact);
            if (!scopedPattern) expected = exact;
        }
        await db.SaveChangesAsync(ct);

        var resolved = await new WorkflowResolver(db).ResolveAsync(type, MessageDirection.Incoming,
            department.Id, branch.Id, ct);
        Assert.Equal(expected.Id, resolved.Id);

        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct),
            await catalog.LoadBranchResolverAsync(ct));
        var file = fixture.File(type: type);
        var routed = processor.Process(new SaaMessageParser().Parse(file));
        Assert.Equal(SaaFileImportResult.Imported, await fixture.Store.RegisterAsync(file, routed, ct));
        Assert.Equal(expected.Id, (await db.Messages.SingleAsync(ct)).WorkflowDefinitionId);
        Assert.Empty(await db.IgnoredImportFiles.ToListAsync(ct));
    }

    [Theory]
    [InlineData("MT4xx", "MT500")]
    [InlineData("MXpacs.008", "MXpacs.009.001.08")]
    [InlineData("MXpacs.008.001.08", "MXpacs.008.001.09")]
    public async Task UnmatchedWorkflowIsAnErrorWithoutImportedOrIgnoredRows(string pattern, string type)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var branch = await db.Branches.SingleAsync(ct);
        var department = await db.Departments.SingleAsync(ct);
        (await db.DepartmentRoutingRules.SingleAsync(ct)).MessageTypePattern = type;
        db.WorkflowDefinitions.Add(new WorkflowDefinition(MessageDirection.Incoming, "Pattern", pattern,
            department.Id).AddStep(1, 1));
        await db.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => new WorkflowResolver(db)
            .ResolveAsync(type, MessageDirection.Incoming, department.Id, branch.Id, ct));

        var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
        var processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(ct),
            await catalog.LoadBranchResolverAsync(ct));
        var file = fixture.File(type: type);
        var routed = processor.Process(new SaaMessageParser().Parse(file));
        await Assert.ThrowsAsync<FormatException>(() => fixture.Store.RegisterAsync(file, routed, ct));
        Assert.Empty(await db.SwiftMessages.ToListAsync(ct));
        Assert.Empty(await db.Messages.ToListAsync(ct));
        Assert.Empty(await db.IgnoredImportFiles.ToListAsync(ct));
    }
}
