using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ORP.Application.Abstractions;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class MockDataModeTests
{
    [Fact]
    public async Task DebugAuthentication_RejectsMissingAndUnknownUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", ct)).StatusCode);

        client.DefaultRequestHeaders.Add("X-Debug-User", "unknown-user");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", ct)).StatusCode);
    }

    [Fact]
    public async Task DebugAuthentication_IsDisabledOutsideDevelopment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", ct)).StatusCode);
    }

    [Fact]
    public async Task Api_StartsWithoutSqlServer_AndServesSeededGridData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory("Development");

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
        var states = (await client.GetFromJsonAsync<List<MessageStateReferenceDto>>("/api/message-states", ct))!;
        Assert.Equal(9, states.Count);
        Assert.Contains(new MessageStateReferenceDto("WaitingForSecondReview", "Waiting for second review"), states);
        Assert.Equal(15, (await client.GetFromJsonAsync<List<WorkflowSummaryDto>>("/api/workflows", ct))!
            .Sum(x => x.Steps.Count));
        var users = (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!;
        Assert.Equal(12, users.Count);
        Assert.Contains(users, x => x.UserName == "amelia.hart" && x.DisplayName == "Amelia Hart");
        Assert.Contains(users, x => x.UserName == "elena.petrova" && x.DisplayName == "Elena Petrova");
        Assert.DoesNotContain(users, x => x.DisplayName.Contains("Reviewer", StringComparison.OrdinalIgnoreCase));

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "amelia.hart");
        Assert.Equal([new ReferenceItemDto(1, "London")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/branches", ct));
        Assert.Equal([new ReferenceItemDto(1, "CS")],
            await client.GetFromJsonAsync<List<ReferenceItemDto>>("/api/departments", ct));
        Assert.Equal(["MT199", "MT700", "MT799"],
            await client.GetFromJsonAsync<List<string>>("/api/message-types", ct));
        Assert.Equal([6, 1, 5, 4],
            (await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users", ct))!.Select(x => x.Id));

        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", "6");
        var currentUser = await client.GetFromJsonAsync<CurrentUserResponse>("/api/me", ct);
        Assert.Equal(6, currentUser!.UserId);
        Assert.Equal("admin", currentUser.UserName);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment)
    {
        var factory = new WebApplicationFactory<Program>();
        return factory.WithWebHostBuilder(web =>
        {
            web.UseEnvironment(environment);
            web.UseSetting("UseMockData", "true");
            web.UseSetting("ConnectionStrings:ORP", "not-a-sql-server-connection");
        });
    }
}
