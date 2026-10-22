using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ORP.Application.Abstractions;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed partial class ConfirmationSqlTests
{
    [Theory]
    [InlineData(1)] // Current executor changes.
    [InlineData(2)] // Only the future assignment and rowversion change.
    [InlineData(3)] // Approval advances the state and active level.
    public async Task DetailsRetryAfterMutationBetweenCapabilitiesAndAssignments(int mutation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        UserAccess access;
        await using (var db = new ORPDbContext(fixture.Options))
            access = (await new UserAccessService(db).GetByIdAsync(setup.Actor, ct))!;
        var interceptor = new MutateBeforeAssignmentRead(async (read, token) =>
        {
            if (read != 1) return;
            await using var writer = new ORPDbContext(fixture.Options);
            if (mutation == 3)
                await Handlers(writer, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), token);
            else
                await AssignHandler(writer, setup.Actor).HandleAsync(setup.Message, new(setup.Assignee, mutation), token);
        });
        await using var reader = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>(fixture.Options)
            .AddInterceptors(interceptor).Options);
        var result = (await new MessageQueries(reader).GetAsync(setup.Message, access, ct))!;
        await using var verify = new ORPDbContext(fixture.Options);
        var expected = (await new MessageQueries(verify).GetAsync(setup.Message, access, ct))!;
        Assert.Equal(expected.Version, result.Version);
        Assert.Equal(expected.State, result.State);
        Assert.Equal(expected.CurrentAssigneeId, result.CurrentAssigneeId);
        Assert.Equal(expected.CanConfirm, result.CanConfirm);
        Assert.Equal(expected.ConfirmationLevel, result.ConfirmationLevel);
        Assert.Equal(expected.Assignments, result.Assignments);
        Assert.Equal(2, interceptor.Reads);
    }

    [Fact]
    public async Task DetailsRejectContinuousMutationsInsteadOfReturningMixedVersions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        UserAccess access;
        await using (var db = new ORPDbContext(fixture.Options))
            access = (await new UserAccessService(db).GetByIdAsync(setup.Actor, ct))!;
        var interceptor = new MutateBeforeAssignmentRead(async (read, token) =>
        {
            await using var writer = new ORPDbContext(fixture.Options);
            await AssignHandler(writer, setup.Actor).HandleAsync(setup.Message,
                new(read % 2 == 1 ? setup.Assignee : setup.Actor, 2), token);
        });
        await using var reader = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>(fixture.Options)
            .AddInterceptors(interceptor).Options);
        await Assert.ThrowsAsync<ConcurrentUpdateException>(() => new MessageQueries(reader).GetAsync(setup.Message, access, ct));
        Assert.Equal(3, interceptor.Reads);
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(setup.Assignee, (await verify.Assignments.SingleAsync(a => a.Level == 2 && a.EndedAt == null, ct)).AssignedTo);
    }

    private sealed class MutateBeforeAssignmentRead(Func<int, CancellationToken, Task> mutate) : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            // The write commits at an explicit point in the read, rather than relying on task timing.
            if (command.CommandText.Contains("FROM [orp].[Assignments]", StringComparison.Ordinal))
                await mutate(++Reads, ct);
            return result;
        }
    }
}
