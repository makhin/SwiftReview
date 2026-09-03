using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SwiftReview.Application.Abstractions;
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

        Assert.Equal(3, (await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/branches", ct))!.Count);
        Assert.Equal(3, (await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/departments", ct))!.Count);
        Assert.Equal(8, (await client.GetFromJsonAsync<List<string>>("/api/message-types", ct))!.Count);
        Assert.Equal(6, (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!.Count);

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "cs-reviewer");
        Assert.Equal([new ReferenceItemDto(1, "London")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/branches", ct));
        Assert.Equal([new ReferenceItemDto(1, "CS")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/departments", ct));
        Assert.Equal(["MT199", "MT700", "MT799"],
            await client.GetFromJsonAsync<List<string>>("/api/message-types", ct));
        Assert.Equal([6, 1, 4, 5],
            (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!.Select(x => x.Id));
    }
}
