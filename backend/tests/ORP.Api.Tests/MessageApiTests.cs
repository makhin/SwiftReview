using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ORP.Api.Tests;

public sealed class MessageApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["UseMockData"] = "true" }));
    }
}

public sealed class MessageApiTests(MessageApiFactory factory) : IClassFixture<MessageApiFactory>
{
    private static readonly string[] RemovedFields =
    [
        "entries", "account", "currency", "amount", "reference", "accounts", "currencies", "amounts",
        "beneficiaryCustomerAccounts", "beneficiaryCustomerBanks", "beneficiaryCustomerNames",
        "orderingCustomerAccounts", "orderingCustomerBanks", "orderingCustomerNames",
        "senderMessageReferences", "settlementDates", "tradeDealDates", "unitDataOwners", "valueDates"
    ];

    [Fact]
    public async Task DetailsAndSearch_ReturnMessagesWithoutEntryFields()
    {
        using var client = CreateClient();
        var ct = TestContext.Current.CancellationToken;
        using var detailResponse = await client.GetAsync("/api/messages/1", ct);
        detailResponse.EnsureSuccessStatusCode();
        var details = await detailResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(1, details.GetProperty("id").GetInt64());
        Assert.False(string.IsNullOrWhiteSpace(details.GetProperty("body").GetString()));
        AssertNoEntryFields(details);

        using var searchResponse = await client.PostAsJsonAsync("/api/messages/search",
            new { skip = 0, take = 10 }, ct);
        searchResponse.EnsureSuccessStatusCode();
        var search = await searchResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(75, search.GetProperty("totalCount").GetInt32());
        Assert.Equal(10, search.GetProperty("items").GetArrayLength());
        foreach (var row in search.GetProperty("items").EnumerateArray()) AssertNoEntryFields(row);
    }

    [Fact]
    public async Task Grid_ReturnsRowsAndTotalWithoutEntryFields()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/api/messages/grid?skip=0&take=10&requireTotalCount=true",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(75, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(10, result.GetProperty("data").GetArrayLength());
        foreach (var row in result.GetProperty("data").EnumerateArray()) AssertNoEntryFields(row);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("currency")]
    [InlineData("amount")]
    [InlineData("reference")]
    public async Task Grid_RejectsRemovedFilterFields(string field)
    {
        using var client = CreateClient();
        var filter = Uri.EscapeDataString(JsonSerializer.Serialize(new object[] { field, "=", "test" }));
        using var response = await client.GetAsync($"/api/messages/grid?skip=0&take=10&filter={filter}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("sum")]
    [InlineData("avg")]
    public async Task Grid_RejectsRemovedAmountSummaries(string summaryType)
    {
        using var client = CreateClient();
        var summary = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { new { selector = "amount", summaryType } }));
        using var response = await client.GetAsync($"/api/messages/grid?skip=0&take=10&totalSummary={summary}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        return client;
    }

    private static void AssertNoEntryFields(JsonElement value)
    {
        foreach (var field in RemovedFields) Assert.False(value.TryGetProperty(field, out _), field);
    }
}
