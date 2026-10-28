using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ORP.Domain.Identity;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed partial class OutgoingEmailHttpSqlTests
{
    [Fact]
    public async Task ForwardingSubjectReadsRulesFreshAndDoesNotMutateMessageOrEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var db = new ORPDbContext(fixture.Options);
        // Retain existing scoped grants and routing while making this the TFO processing department.
        await db.Departments.ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "TFO"), ct);
        await db.SwiftMessages.ExecuteUpdateAsync(s => s.SetProperty(x => x.RelatedReference, "reference-21"), ct);
        var seed = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-forwarding-subject-rules.sql"), ct);
        await ExecuteSubjectSeed(fixture.ConnectionString, seed, ct);
        var before = await db.Messages.AsNoTracking().SingleAsync(ct);
        var auditCount = await db.AuditEvents.CountAsync(ct);
        await using var factory = new Factory(fixture.ConnectionString, from: "");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-scoped");
        var url = $"/api/messages/{id}/forwarding-subject";
        using var response = await client.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Equal("FW: MT199 - reference-21", await response.Content.ReadAsStringAsync(ct));
        await db.ForwardingSubjectRules.ExecuteUpdateAsync(s => s.SetProperty(x => x.Template, "Changed: {Field21}"), ct);
        Assert.Equal("Changed: reference-21", await client.GetStringAsync(url, ct));
        await db.ForwardingSubjectRules.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        Assert.Equal("FW: MT199", await client.GetStringAsync(url, ct));
        var after = await db.Messages.AsNoTracking().SingleAsync(ct);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(before.State, after.State);
        Assert.Equal(before.CurrentAssigneeId, after.CurrentAssigneeId);
        Assert.Empty(await db.OutgoingEmails.ToListAsync(ct));
        Assert.Equal(auditCount, await db.AuditEvents.CountAsync(ct));
    }

    [Fact]
    public async Task ForwardingSubjectEnforcesForwardPermissionAndViewOnlyPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var id = await Seed(fixture);
        await using var factory = new Factory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        async Task Check(long messageId, HttpStatusCode expected)
        {
            using var response = await client.GetAsync($"/api/messages/{messageId}/forwarding-subject", ct);
            Assert.Equal(expected, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        await Check(id, HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-outsider");
        await Check(id, HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-scoped");
        await using var db = new ORPDbContext(fixture.Options);
        var forwardId = await db.Permissions.Where(x => x.Name == Permissions.MessageForward).Select(x => x.Id).SingleAsync(ct);
        await db.RolePermissions.Where(x => x.PermissionId == forwardId).ExecuteDeleteAsync(ct);
        await Check(id, HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "email-admin");
        await Check(long.MaxValue, HttpStatusCode.NotFound);
        var workflow = new WorkflowDefinition("SUBJECT_VIEW_ONLY", "View only");
        workflow.ConfigureConfirmation(ConfirmationMode.None, false, AssignmentRetentionMode.PerConfirmation, false);
        db.Add(workflow);
        await db.SaveChangesAsync(ct);
        var message = await db.Messages.Include(x => x.Departments).SingleAsync(ct);
        message.ApplyRouting(workflow, []);
        await db.SaveChangesAsync(ct);
        await Check(id, HttpStatusCode.Conflict);
        Assert.Empty(await db.OutgoingEmails.ToListAsync(ct));
    }

    [Fact]
    public async Task ForwardingSubjectMigrationAndSeedHandleExistingAndMissingTfo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        Assert.Empty(await db.ForwardingSubjectRules.ToListAsync(ct));
        var seed = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-forwarding-subject-rules.sql"), ct);
        var error = await Assert.ThrowsAsync<SqlException>(() => ExecuteSubjectSeed(fixture.ConnectionString, seed, ct));
        Assert.Equal(50001, error.Number);
        db.Add(new Department("TFO"));
        await db.SaveChangesAsync(ct);
        // Exercise both upgrade with an existing TFO and rollback on an isolated test database.
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, x => x.EndsWith("_AddForwardingSubjectRules", StringComparison.Ordinal));
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[index - 1], ct);
        await migrator.MigrateAsync(cancellationToken: ct);
        var rule = await db.ForwardingSubjectRules.SingleAsync(ct);
        Assert.Equal("FW: {MessageType} - {Field21}", rule.Template);
        Assert.Equal(100, rule.Priority);
        Assert.True(rule.IsActive);
        Assert.Null(rule.BranchId);
        Assert.Null(rule.MessageType);
        rule.Template = "Customized: {Field21}";
        await db.SaveChangesAsync(ct);
        await ExecuteSubjectSeed(fixture.ConnectionString, seed, ct);
        await ExecuteSubjectSeed(fixture.ConnectionString, seed, ct);
        Assert.Equal("Customized: {Field21}", await db.ForwardingSubjectRules.AsNoTracking().Select(x => x.Template).SingleAsync(ct));
    }
    private static async Task ExecuteSubjectSeed(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

}
