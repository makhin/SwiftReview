using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Reviews;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class DeleteMessageRangeSqlTests
{
    [Theory]
    [InlineData("delete")]
    [InlineData("preview")]
    [InlineData("empty")]
    [InlineData("invalid")]
    [InlineData("rollback")]
    public async Task DeletesOnlyInclusiveRangeAndRollsBackOnFailure(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        for (var i = 0; i < 4; i++)
        {
            var file = fixture.File(name: $"range-{i}.prt");
            await fixture.Store.RegisterAsync(file, fixture.Route(file), ct);
        }
        long[] ids;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            ids = await db.Messages.OrderBy(m => m.Id).Select(m => m.Id).ToArrayAsync(ct);
            var user = new User("range-reviewer", "Range reviewer");
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            foreach (var id in ids)
            {
                var review = new Review(id, 1, user.Id, DateTimeOffset.UtcNow);
                review.Approve("Checked", DateTimeOffset.UtcNow);
                db.Reviews.Add(review);
                db.Assignments.Add(new Assignment(id, user.Id, user.Id, DateTimeOffset.UtcNow));
                db.AuditEvents.Add(new AuditEvent(id, AuditEventType.ReviewApproved, user.Id,
                    DateTimeOffset.UtcNow, null, null, "{}", "range-test", review));
            }
            foreach (var source in await db.SwiftMessages.ToListAsync(ct))
                db.IgnoredImportFiles.Add(new IgnoredImportFile
                {
                    FilePath = source.FilePath, ContentHash = source.ContentHash,
                    IgnoredAtUtc = DateTimeOffset.UtcNow, Reason = "NoDepartmentRule"
                });
            await db.SaveChangesAsync(ct);
            if (scenario == "rollback")
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TRIGGER orp.FailRangeDelete ON orp.SwiftMessages AFTER DELETE AS
                    BEGIN
                        THROW 51001, 'Injected deletion failure', 1;
                    END
                    """, ct);
        }
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "delete-messages-by-id-range.sql"), ct);
        var from = scenario == "empty" ? ids[^1] + 1 : ids[1];
        var to = scenario == "empty" ? ids[^1] + 2 : scenario == "invalid" ? ids[0] : ids[2];
        script = script.Replace("DECLARE @FromId bigint = NULL;", $"DECLARE @FromId bigint = {from};")
            .Replace("DECLARE @ToId bigint = NULL;", $"DECLARE @ToId bigint = {to};")
            .Replace("DECLARE @DryRun bit = 1;", $"DECLARE @DryRun bit = {(scenario == "preview" ? 1 : 0)};");
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(script, connection);
        if (scenario is "invalid" or "rollback")
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(ct));
            Assert.Equal(scenario == "invalid" ? 51000 : 51001, error.Number);
        }
        else
            await command.ExecuteNonQueryAsync(ct);

        await using var verify = new ORPDbContext(fixture.Options);
        var expected = scenario == "delete" ? new[] { ids[0], ids[3] } : ids;
        Assert.Equal(expected, await verify.Messages.OrderBy(m => m.Id).Select(m => m.Id).ToArrayAsync(ct));
        Assert.Equal(expected, await verify.SwiftMessages.OrderBy(m => m.MessageId).Select(m => m.MessageId).ToArrayAsync(ct));
        Assert.Equal(expected, await verify.Reviews.OrderBy(r => r.MessageId).Select(r => r.MessageId).ToArrayAsync(ct));
        Assert.Equal(expected, await verify.Assignments.OrderBy(a => a.MessageId).Select(a => a.MessageId).ToArrayAsync(ct));
        Assert.Equal(expected, await verify.MessageDepartments.OrderBy(d => d.MessageId).Select(d => d.MessageId).ToArrayAsync(ct));
        Assert.Equal(expected, await verify.AuditEvents.Select(a => a.MessageId).Distinct().OrderBy(id => id).ToArrayAsync(ct));
        Assert.Equal(expected.Length * 2, await verify.AuditEvents.CountAsync(ct));
        var expectedPaths = scenario == "delete" ? new[] { "range-0.prt", "range-3.prt" }
            : new[] { "range-0.prt", "range-1.prt", "range-2.prt", "range-3.prt" };
        Assert.Equal(expectedPaths, await verify.IgnoredImportFiles.OrderBy(f => f.FilePath).Select(f => f.FilePath).ToArrayAsync(ct));
        Assert.True(await verify.Users.AnyAsync(u => u.UserName == "range-reviewer", ct));
        Assert.True(await verify.WorkflowDefinitions.AnyAsync(w => w.Code == "TEST_SINGLE", ct));
        Assert.Equal(1, await verify.DepartmentRoutingRules.CountAsync(ct));
    }
}
