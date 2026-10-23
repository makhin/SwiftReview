using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed partial class ConfirmationSqlTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task BulkApprovalQueryBudget(int size)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        long[] ids;
        await using (var seed = new ORPDbContext(fixture.Options))
        {
            await seed.Users.Where(u => u.Id == setup.Actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsGlobalAdministrator, true), ct);
            ids = await seed.Messages.OrderBy(m => m.Id).Select(m => m.Id).Take(size).ToArrayAsync(ct);
        }
        Assert.Equal(size, ids.Length);
        await ConfigureBulkAsync(fixture, ids, [1, 2, 3], AssignmentRetentionMode.PerConfirmation, ct);
        var counter = new BulkQueryCounter();
        await using var db = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>(fixture.Options).AddInterceptors(counter).Options);
        var started = Stopwatch.GetTimestamp();
        var response = await BulkHandler(db, setup.Actor).HandleAsync(new(ids), ct);
        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.All(response.Results, result => Assert.Equal(200, result.Status));
        TestContext.Current.TestOutputHelper!.WriteLine($"Batch {size}: permission queries={counter.Permissions}, reader commands={counter.Readers}, elapsed={elapsed.TotalMilliseconds:F0} ms");
        Assert.Equal(size, counter.Permissions);
    }

    private sealed class BulkQueryCounter : DbCommandInterceptor
    {
        public int Readers { get; private set; }
        public int Permissions { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Readers++;
            if (command.CommandText.Contains("[IsGlobalAdministrator]") && command.CommandText.Contains("[Permissions]")) Permissions++;
            return ValueTask.FromResult(result);
        }
    }
}
