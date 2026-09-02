using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SwiftReview.IntegrationTests;

public sealed class MockDataModeTests
{
    [Fact]
    public async Task Api_StartsWithoutSqlServer_AndServesSeededGridData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseMockData"] = "true",
                ["ConnectionStrings:SwiftReview"] = "not-a-sql-server-connection"
            }));
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "supervisor");

        using var response = await client.GetAsync("/api/messages/grid?skip=0&take=10&requireTotalCount=true", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(10, json.RootElement.GetProperty("data").GetArrayLength());
        Assert.Equal(75, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.All(json.RootElement.GetProperty("data").EnumerateArray(), row =>
            Assert.StartsWith("MSG-", row.GetProperty("externalId").GetString()));
    }
}
