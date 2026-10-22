using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ORP.Application.Abstractions;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed partial class ConfirmationSqlTests
{
    [Fact]
    public async Task CardRetriesWhenConfirmationCommitsAfterDetailsWereRead()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        UserAccess access;
        await using (var db = new ORPDbContext(fixture.Options))
            access = (await new UserAccessService(db).GetByIdAsync(setup.Actor, ct))!;
        var interceptor = new MutateBeforeCardAudit(async (read, token) =>
        {
            if (read != 1) return;
            await using var writer = new ORPDbContext(fixture.Options);
            await Handlers(writer, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), token);
        });
        await using var reader = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>(fixture.Options)
            .AddInterceptors(interceptor).Options);
        var actual = (await new MessageViewQueries(reader, new MessageQueries(reader)).GetAsync(setup.Message, access, ct))!;
        await using var verify = new ORPDbContext(fixture.Options);
        var expected = (await new MessageViewQueries(verify, new MessageQueries(verify)).GetAsync(setup.Message, access, ct))!;
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Approvals, actual.Approvals);
        Assert.Equal(expected.AuditLog, actual.AuditLog);
        Assert.Equal(2, interceptor.Reads);
    }

    [Fact]
    public async Task CardRejectsContinuousMutationsAfterDetailsWereRead()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        UserAccess access;
        await using (var db = new ORPDbContext(fixture.Options))
            access = (await new UserAccessService(db).GetByIdAsync(setup.Actor, ct))!;
        var interceptor = new MutateBeforeCardAudit(async (read, token) =>
        {
            await using var writer = new ORPDbContext(fixture.Options);
            await AssignHandler(writer, setup.Actor).HandleAsync(setup.Message,
                new(read % 2 == 1 ? setup.Assignee : setup.Actor, 2), token);
        });
        await using var reader = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>(fixture.Options)
            .AddInterceptors(interceptor).Options);
        await Assert.ThrowsAsync<ConcurrentUpdateException>(() =>
            new MessageViewQueries(reader, new MessageQueries(reader)).GetAsync(setup.Message, access, ct));
        Assert.Equal(3, interceptor.Reads);
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(setup.Assignee, (await verify.Assignments.SingleAsync(a => a.Level == 2 && a.EndedAt == null, ct)).AssignedTo);
    }

    [Fact]
    public async Task InvalidReviewLevelsReturnBadRequestForOwnerOtherUserAndAdministrator()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        await using var db = new ORPDbContext(fixture.Options);
        var admin = await db.Users.Where(u => u.IsGlobalAdministrator).Select(u => u.Id).SingleAsync(ct);
        foreach (var actor in new[] { setup.Actor, setup.Assignee, admin })
        foreach (var action in new[] { "start", "approve", "reject", "cancel", "confirm", "assign-and-confirm" })
        {
            client.DefaultRequestHeaders.Remove("X-Debug-User");
            client.DefaultRequestHeaders.Add("X-Debug-User", actor.ToString());
            using var response = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/{action}",
                new { level = 4, reviewId = 1 }, ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Empty(await db.Reviews.ToListAsync(ct));
    }

    private sealed class MutateBeforeCardAudit(Func<int, CancellationToken, Task> mutate) : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("FROM [orp].[AuditEvents]", StringComparison.Ordinal) &&
                command.CommandText.Contains("COUNT(", StringComparison.Ordinal))
                await mutate(++Reads, ct);
            return result;
        }
    }
}
