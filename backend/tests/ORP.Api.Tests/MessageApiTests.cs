using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ORP.Api.Tests;

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
        using var detailResponse = await client.GetAsync($"/api/messages/{factory.MessageId(1)}", ct);
        detailResponse.EnsureSuccessStatusCode();
        var details = await detailResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(factory.MessageId(1), details.GetProperty("id").GetInt64());
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

    [Theory]
    [InlineData("")]
    [InlineData("&filter=%5B%5D")]
    public async Task Grid_ReturnsRowsAndTotalWithoutEntryFields(string filterQuery)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync($"/api/messages/grid?skip=0&take=10&requireTotalCount=true{filterQuery}",
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

    [Theory]
    [InlineData("[\"id\",{0}]")]
    [InlineData("[\"id\",\"=\",{0}]")]
    [InlineData("[[\"id\",\">=\",{0}],[\"id\",\"<\",{3}]]")]
    [InlineData("[\"!\",[\"id\",\"<>\",{0}]]")]
    [InlineData("[[\"id\",{0}],\"or\",[[\"id\",{1}],\"and\",[\"id\",{2}]]]")]
    public async Task Grid_AcceptsDevExtremeFilterSyntax(string expression)
    {
        using var client = CreateClient();
        var filter = Uri.EscapeDataString(string.Format(System.Globalization.CultureInfo.InvariantCulture, expression,
            factory.MessageId(1), factory.MessageId(2), factory.MessageId(3), factory.MessageId(1) + 1));
        var result = await client.GetFromJsonAsync<JsonElement>(
            $"/api/messages/grid?skip=0&take=10&requireTotalCount=true&filter={filter}", TestContext.Current.CancellationToken);
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(factory.MessageId(1), Assert.Single(result.GetProperty("data").EnumerateArray()).GetProperty("id").GetInt64());
    }

    [Theory]
    [InlineData("[\"id\"]")]
    [InlineData("[\"id\",\"unsupported\",1]")]
    [InlineData("[\"!\",[\"currency\",\"EUR\"]]")]
    public async Task Grid_InvalidFiltersReturnBadRequest(string expression)
    {
        using var client = CreateClient();
        var filter = Uri.EscapeDataString(expression);
        using var response = await client.GetAsync($"/api/messages/grid?skip=0&take=10&filter={filter}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grid_UsesDevExtremeSummariesAndSelection()
    {
        using var client = CreateClient();
        var filter = Uri.EscapeDataString($"[[\"id\",{factory.MessageId(1)}],\"or\",[\"id\",{factory.MessageId(2)}]]");
        var summary = Uri.EscapeDataString("[{\"selector\":\"id\",\"summaryType\":\"sum\"},{\"selector\":\"id\",\"summaryType\":\"avg\"}]");
        var select = Uri.EscapeDataString("[\"id\"]");
        var result = await client.GetFromJsonAsync<JsonElement>(
            $"/api/messages/grid?skip=0&take=10&filter={filter}&totalSummary={summary}&select={select}", TestContext.Current.CancellationToken);
        Assert.Equal(factory.MessageId(1) + factory.MessageId(2), result.GetProperty("summary")[0].GetDecimal());
        Assert.Equal((factory.MessageId(1) + factory.MessageId(2)) / 2m, result.GetProperty("summary")[1].GetDecimal());
        Assert.All(result.GetProperty("data").EnumerateArray(), row => Assert.Equal("id", Assert.Single(row.EnumerateObject()).Name));
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
