using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class SqlServerAdministrationTests
{
    [Fact]
    public async Task UserGrid_ExecutesSortingSearchAndPagingOnSqlServer()
    {
        await using var factory = new MessageApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<ORPDbContext>().Database.IsSqlServer());
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        var ct = TestContext.Current.CancellationToken;
        var first = await client.GetFromJsonAsync<JsonElement>("/api/admin/users/grid?take=1", ct);
        Assert.True(first.GetProperty("totalCount").GetInt32() >= 2);
        Assert.Single(first.GetProperty("data").EnumerateArray());
        var second = await client.GetFromJsonAsync<JsonElement>("/api/admin/users/grid?skip=1&take=1", ct);
        Assert.NotEqual(first.GetProperty("data")[0].GetProperty("id").GetInt32(),
            second.GetProperty("data")[0].GetProperty("id").GetInt32());
        var sort = Uri.EscapeDataString("[{\"selector\":\"userName\",\"desc\":true}]");
        var sorted = await client.GetFromJsonAsync<JsonElement>($"/api/admin/users/grid?take=100&sort={sort}", ct);
        var names = sorted.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("userName").GetString()!).ToArray();
        Assert.Equal(names.OrderDescending(StringComparer.OrdinalIgnoreCase), names);
        var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/admin/users/grid?search=amelia.hart&sort={sort}", ct);
        Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
        Assert.Equal("amelia.hart", filtered.GetProperty("data")[0].GetProperty("userName").GetString());
    }
}
