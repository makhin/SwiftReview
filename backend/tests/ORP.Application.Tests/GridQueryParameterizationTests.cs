using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using Xunit;

namespace ORP.Application.Tests;

// ToQueryString exercises the SQL Server provider without opening a database connection.
public sealed class GridQueryParameterizationTests
{
    [Theory]
    [InlineData("externalId", "eq", "\"Alice\"", "\"Bob\"")]
    [InlineData("externalId", "ne", "\"Alice\"", "\"Bob\"")]
    [InlineData("externalId", "contains", "\"Alice\"", "\"Bob\"")]
    [InlineData("externalId", "startsWith", "\"Alice\"", "\"Bob\"")]
    [InlineData("externalId", "endsWith", "\"Alice\"", "\"Bob\"")]
    [InlineData("id", "eq", "10000000000", "20000000000")]
    [InlineData("branchId", "gt", "1", "2")]
    [InlineData("branchId", "gte", "1", "2")]
    [InlineData("branchId", "lt", "1", "2")]
    [InlineData("branchId", "lte", "1", "2")]
    [InlineData("currentAssigneeId", "eq", "1", "2")]
    [InlineData("activeReviewId", "eq", "10000000000", "20000000000")]
    [InlineData("state", "eq", "\"Assigned\"", "\"Completed\"")]
    [InlineData("direction", "eq", "\"Incoming\"", "\"Outgoing\"")]
    [InlineData("receivedAt", "gte", "\"2026-10-06T08:00:00Z\"", "\"2026-10-07T08:00:00Z\"")]
    public void MessageFiltersReuseSqlAndCompilationWhenValuesChange(string field, string op, string first, string second)
    {
        var compilations = 0;
        using var db = CreateContext(() => compilations++);
        string Sql(string json)
        {
            var options = GridQuery<MessageGridRowDto>.Create(new GridRequest(Filter: Condition(field, op, json)),
                GridFields.Messages, 500, 5, new SortClause("receivedAt", "desc"));
            return options.Page(options.Filter(db.Set<MessageGridRowDto>())).ToQueryString();
        }
        var firstSql = Sql(first);
        Assert.Equal(1, compilations);
        var secondSql = Sql(second);
        Assert.Equal(1, compilations);
        Assert.NotEqual(firstSql, secondSql); // Parameter declarations contain the actual request values.
        Assert.Equal(Statement(firstSql), Statement(secondSql));
        Assert.Matches(@"WHERE[^\r\n]+@", Statement(firstSql));
    }

    [Fact]
    public void UserFiltersReuseSqlAndCompilationWhenValuesChange()
    {
        var compilations = 0;
        using var db = CreateContext(() => compilations++);
        string Sql(string name)
        {
            var options = GridQuery<AdminUserGridRow>.Create(new AdminUserGridRequest(Filter: Condition("displayName", "eq", JsonSerializer.Serialize(name))),
                GridFields.Users, 100, 3, new SortClause("displayName", "asc"));
            return options.Page(options.Filter(db.Set<AdminUserGridRow>())).ToQueryString();
        }
        var firstSql = Sql("Alice");
        var secondSql = Sql("Bob");
        Assert.Equal(1, compilations);
        Assert.Equal(Statement(firstSql), Statement(secondSql));
        Assert.Matches(@"WHERE[^\r\n]+@", Statement(firstSql));
        Assert.DoesNotContain("Alice", Statement(firstSql));
        Assert.DoesNotContain("Bob", Statement(secondSql));
    }

    [Fact]
    public void FlatMessageControlsTranslateToParameterizedSql()
    {
        var compilations = 0;
        using var db = CreateContext(() => compilations++);
        string Sql(string search, string branch)
        {
            var request = MessageGridOptions.Create(new(Search: search, Status: "New", MessageType: "MT199",
                Branch: branch, DateFrom: "2026-10-01", DateTo: "2026-10-09", Page: 2, PageSize: 5));
            var options = GridQuery<MessageGridRowDto>.Create(request, GridFields.Messages, 500, 5, new("receivedAt", "desc"));
            return options.Page(options.Filter(db.Set<MessageGridRowDto>())).ToQueryString();
        }
        var first = Sql("Alice", "1");
        var second = Sql("Bob", "2");
        Assert.Equal(1, compilations);
        Assert.Equal(Statement(first), Statement(second));
        Assert.Contains(" OR ", first);
        Assert.Contains("OFFSET", first);
        Assert.Contains("FETCH NEXT", first);
        Assert.DoesNotContain("Alice", Statement(first));
    }

    private static GridFilter Condition(string field, string op, string json) => new()
    {
        Field = field, Operator = op, Value = JsonSerializer.Deserialize<JsonElement>(json)
    };

    private static string Statement(string sql) => sql[sql.IndexOf("SELECT ", StringComparison.Ordinal)..];

    private static QueryContext CreateContext(Action compiled) => new(new DbContextOptionsBuilder<QueryContext>()
        .UseSqlServer("Server=localhost;Database=GridQueryParameterizationTests;Integrated Security=true")
        .EnableServiceProviderCaching(false)
        .LogTo(_ => compiled(), [CoreEventId.QueryCompilationStarting]).Options);

    private sealed class QueryContext(DbContextOptions<QueryContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MessageGridRowDto>().Ignore(row => row.RequiredReviewLevels);
            modelBuilder.Entity<AdminUserGridRow>();
        }
    }
}
