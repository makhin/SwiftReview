using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ORP.Application.Abstractions;
using ORP.Domain.Messages;
using Xunit;

namespace ORP.Api.Tests;

public sealed class MessageStateCountsApiTests : IDisposable
{
    private readonly MessageApiFactory factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public void Dispose() => factory.Dispose();
    private HttpClient Client(string user)
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Debug-User", user); return client;
    }
    private static async Task<MessageStateCountDto[]> Counts(HttpClient client) =>
        (await client.GetFromJsonAsync<MessageStateCountDto[]>("/api/messages/state-counts", Json, Ct))!;

    [Fact]
    public async Task Counts_CoverEveryEnumStateAndAllPages_WithinViewScope()
    {
        using var user = Client("amelia.hart"); using var admin = Client("admin");
        var counts = await Counts(user);
        Assert.Equal(Enum.GetValues<MessageState>(), counts.Select(c => c.State));
        var states = await user.GetFromJsonAsync<MessageStateReferenceDto[]>("/api/message-states", Ct);
        Assert.Equal(Enum.GetNames<MessageState>(), states!.Select(s => s.Code));
        var grid = await user.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=1&requireTotalCount=true", Ct);
        Assert.Equal(grid.GetProperty("totalCount").GetInt32(), counts.Sum(c => c.Count));
        Assert.True(counts.Sum(c => c.Count) > 1);
        Assert.All(counts.Where(c => c.State != MessageState.New), c => Assert.Equal(0, c.Count));
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        var after = await Counts(user);
        Assert.Equal(counts.Single(c => c.State == MessageState.New).Count - 1, after.Single(c => c.State == MessageState.New).Count);
        Assert.Equal(1, after.Single(c => c.State == MessageState.Assigned).Count);
        var filter = Uri.EscapeDataString("[\"state\",\"=\",\"Assigned\"]");
        var filtered = await user.GetFromJsonAsync<JsonElement>($"/api/messages/grid?skip=0&take=100&requireTotalCount=true&filter={filter}", Ct);
        Assert.Equal(after.Single(c => c.State == MessageState.Assigned).Count, filtered.GetProperty("totalCount").GetInt32());
        (await admin.PutAsJsonAsync("/api/admin/users/1/access", new { assignments = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
        Assert.All(await Counts(user), c => Assert.Equal(0, c.Count));
    }

    [Fact]
    public async Task GlobalAdministratorWithoutRoles_CountsAllVisibleMessages()
    {
        using var admin = Client("admin"); using var user = Client("amelia.hart");
        (await admin.PutAsJsonAsync("/api/admin/users/5/access", new { assignments = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
        var all = await Counts(admin); var scoped = await Counts(user);
        Assert.True(all.Sum(c => c.Count) > scoped.Sum(c => c.Count));
        var grid = await admin.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=1&requireTotalCount=true", Ct);
        Assert.Equal(grid.GetProperty("totalCount").GetInt32(), all.Sum(c => c.Count));
    }
}
