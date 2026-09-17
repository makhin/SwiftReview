using System.Net.Http.Json;
using System.Text.Json;
using ORP.Application.Abstractions;
using Xunit;

namespace ORP.Api.Tests;

public sealed class ReviewQueueApiTests : IAsyncLifetime
{
    private readonly MessageApiFactory factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public ValueTask InitializeAsync() => factory.InitializeAsync();
    public ValueTask DisposeAsync() => factory.DisposeAsync();
    private HttpClient Client(string name)
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Debug-User", name); return client;
    }
    private async Task<JsonElement> Row(HttpClient client)
    {
        var grid = await client.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=100", Ct);
        return grid.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetInt64() == factory.MessageId(1));
    }
    [Theory]
    [InlineData("approve", "Completed")]
    [InlineData("reject", "Rejected")]
    public async Task FullQueue_RetainsFinishedMessagesAndAuditWithinViewScope(string action, string state)
    {
        using var admin = Client("admin"); using var user = Client("amelia.hart");
        Assert.False((await Row(user)).GetProperty("canReview").GetBoolean());
        (await admin.PostAsJsonAsync($"/api/messages/{factory.MessageId(1)}/assign", new { assignedTo = factory.UserId("amelia.hart") }, Ct)).EnsureSuccessStatusCode();
        Assert.True((await Row(user)).GetProperty("canReview").GetBoolean());
        var start = await user.PostAsJsonAsync($"/api/messages/{factory.MessageId(1)}/reviews/start", new { level = 1 }, Ct);
        var id = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
        (await user.PostAsJsonAsync($"/api/messages/{factory.MessageId(1)}/reviews/{action}", new { level = 1, reviewId = id }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/roles/{factory.RoleId("CS Reviewer")}/permissions", new { permissions = new[] { "message.view", "audit.view" } }, Ct)).EnsureSuccessStatusCode();
        var row = await Row(user);
        Assert.Equal(state, row.GetProperty("state").GetString());
        Assert.False(row.GetProperty("canReview").GetBoolean());
        (await user.GetAsync($"/api/messages/{factory.MessageId(1)}/audit", Ct)).EnsureSuccessStatusCode();
        var grid = await user.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=100", Ct);
        Assert.All(grid.GetProperty("data").EnumerateArray(), r => { Assert.Equal(factory.BranchId("London"), r.GetProperty("branchId").GetInt32()); Assert.Equal(factory.DepartmentId("CS"), r.GetProperty("departmentId").GetInt32()); });
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReviewAvailability_RequiresCurrentLevelAndOwner(int level)
    {
        using var admin = Client("admin"); using var user = Client("amelia.hart");
        (await admin.PutAsJsonAsync($"/api/messages/{factory.MessageId(1)}/workflow", new { workflowDefinitionId = factory.WorkflowId("MT671") }, Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/messages/{factory.MessageId(1)}/assign", new { assignedTo = factory.UserId("admin") }, Ct)).EnsureSuccessStatusCode();
        for (var previous = 1; previous < level; previous++)
        {
            var start = await admin.PostAsJsonAsync($"/api/messages/{factory.MessageId(1)}/reviews/start", new { level = previous }, Ct);
            var id = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
            (await admin.PostAsJsonAsync($"/api/messages/{factory.MessageId(1)}/reviews/approve", new { level = previous, reviewId = id }, Ct)).EnsureSuccessStatusCode();
        }
        (await admin.PutAsJsonAsync($"/api/admin/roles/{factory.RoleId("CS Reviewer")}/permissions", new { permissions = new[] { "message.view", $"review.level{level}" } }, Ct)).EnsureSuccessStatusCode();
        Assert.False((await Row(user)).GetProperty("canReview").GetBoolean());
        var assign = level == 1 ? "reassign" : "assign";
        (await admin.PostAsJsonAsync($"/api/messages/{factory.MessageId(1)}/{assign}", new { assignedTo = factory.UserId("amelia.hart") }, Ct)).EnsureSuccessStatusCode();
        Assert.True((await Row(user)).GetProperty("canReview").GetBoolean());
        (await admin.PutAsJsonAsync($"/api/admin/roles/{factory.RoleId("CS Reviewer")}/permissions", new { permissions = new[] { "message.view", $"review.level{(level == 1 ? 2 : 1)}" } }, Ct)).EnsureSuccessStatusCode();
        Assert.False((await Row(user)).GetProperty("canReview").GetBoolean());
        Assert.True((await Row(admin)).GetProperty("canReview").GetBoolean());
    }
}
