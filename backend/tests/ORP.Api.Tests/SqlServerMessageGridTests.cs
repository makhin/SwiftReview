using System.Collections.Concurrent;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ORP.Api.Tests;

public sealed class SqlServerMessageGridTests
{
    [Fact]
    public async Task ScopedGrid_ExecutesFilterSortAndPagingInSqlServer()
    {
        var commands = new CapturedReads();
        await using var factory = new MessageApiFactory(commands);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "amelia.hart");
        var filter = Uri.EscapeDataString("[[\"state\",\"=\",\"New\"],\"and\",[\"messageType\",\"=\",\"MT199\"]]");
        var sort = Uri.EscapeDataString("[{\"selector\":\"externalId\",\"desc\":true}]");
        commands.Reads.Clear();
        var result = await client.GetFromJsonAsync<JsonElement>(
            $"/api/messages/grid?skip=1&take=2&requireTotalCount=true&filter={filter}&sort={sort}", TestContext.Current.CancellationToken);

        // The SQL seed gives Amelia access to London/CS: MT199 messages 1, 25, 49, 73.
        Assert.Equal(4, result.GetProperty("totalCount").GetInt32());
        var rows = result.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(new[] { factory.MessageId(49), factory.MessageId(25) }, rows.Select(r => r.GetProperty("id").GetInt64()));
        Assert.All(rows, row =>
        {
            Assert.Equal(factory.BranchId("London"), row.GetProperty("branchId").GetInt32());
            Assert.Equal(factory.DepartmentId("CS"), row.GetProperty("departmentId").GetInt32());
            Assert.Equal("New", row.GetProperty("state").GetString());
            Assert.Equal("MT199", row.GetProperty("messageType").GetString());
            Assert.False(row.GetProperty("canReview").GetBoolean());
        });

        // Assert server-side execution, not just correct results after client-side loading.
        var paged = Assert.Single(commands.Reads, r => r.Sql.Contains("OFFSET", StringComparison.Ordinal));
        Assert.Contains("FETCH NEXT", paged.Sql);
        Assert.Contains("ORDER BY", paged.Sql);
        Assert.Contains("DESC", paged.Sql);
        Assert.Contains("WHERE", paged.Sql);
        Assert.Contains("[State]", paged.Sql);
        Assert.Contains("[MessageType]", paged.Sql);
        Assert.Contains("[UserRoles]", paged.Sql);
        Assert.True(paged.Sql.Contains("MT199", StringComparison.Ordinal) || paged.Parameters.Contains("MT199"));
        Assert.True(paged.Sql.Contains("New", StringComparison.Ordinal) || paged.Parameters.Contains("New"));
        Assert.Contains(commands.Reads, r => r.Sql.Contains("COUNT(", StringComparison.Ordinal));
    }

    private sealed record Read(string Sql, object?[] Parameters);
    private sealed class CapturedReads : DbCommandInterceptor
    {
        public ConcurrentQueue<Read> Reads { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads.Enqueue(new Read(command.CommandText, command.Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
