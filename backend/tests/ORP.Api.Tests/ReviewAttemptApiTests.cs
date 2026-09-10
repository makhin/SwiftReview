using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Domain.Reviews;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class ReviewAttemptApiTests : IDisposable
{
    private readonly MessageApiFactory factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public void Dispose() => factory.Dispose();
    private HttpClient Client(string user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", user);
        return client;
    }
    private static async Task<long> Start(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
    }

    [Theory]
    [InlineData("approve", false, false)]
    [InlineData("reject", false, false)]
    [InlineData("cancel", false, false)]
    [InlineData("approve", true, false)]
    [InlineData("reject", true, false)]
    [InlineData("cancel", true, false)]
    [InlineData("approve", false, true)]
    [InlineData("reject", false, true)]
    [InlineData("cancel", false, true)]
    [InlineData("approve", true, true)]
    [InlineData("reject", true, true)]
    [InlineData("cancel", true, true)]
    public async Task OldDecision_CannotAffectRestartedAttempt(string action, bool undo, bool administrator)
    {
        using var admin = Client("admin");
        using var owner = Client("amelia.hart");
        var actor = administrator ? admin : owner;
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        var oldId = await Start(owner);
        (await admin.PostAsJsonAsync($"/api/messages/1/reviews/{(undo ? "approve" : "cancel")}", new { level = 1, reviewId = oldId }, Ct)).EnsureSuccessStatusCode();
        if (undo)
        {
            (await admin.PostAsJsonAsync("/api/messages/1/undo", new { reviewId = oldId }, Ct)).EnsureSuccessStatusCode();
            (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        }
        var newId = await Start(owner);
        Assert.NotEqual(oldId, newId);
        Assert.Equal(newId, await Start(owner));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var count = await db.AuditEvents.CountAsync(Ct);
        var response = await actor.PostAsJsonAsync($"/api/messages/1/reviews/{action}", new { level = 1, reviewId = oldId, comment = "Stale decision" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("no longer active", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(ReviewStatus.InProgress, (await db.Reviews.AsNoTracking().SingleAsync(r => r.Id == newId, Ct)).Status);
        Assert.Equal(count, await db.AuditEvents.CountAsync(Ct));
        (await actor.PostAsJsonAsync($"/api/messages/1/reviews/{action}", new { level = 1, reviewId = newId }, Ct)).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("cancel")]
    public async Task Decision_RequiresValidIdMatchingMessageAndLevel(string action)
    {
        using var admin = Client("admin");
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 5 }, Ct)).EnsureSuccessStatusCode();
        var id = await Start(admin);
        var path = $"/api/messages/1/reviews/{action}";
        foreach (var invalidId in new long[] { 0, -1 })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, new { level = 1, reviewId = invalidId }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, new { level = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path, new { level = 2, reviewId = id }, Ct)).StatusCode);
        (await admin.PostAsJsonAsync("/api/messages/2/assign", new { assignedTo = 5 }, Ct)).EnsureSuccessStatusCode();
        var other = await admin.PostAsJsonAsync("/api/messages/2/reviews/start", new { level = 1 }, Ct);
        var otherId = (await other.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path, new { level = 1, reviewId = otherId }, Ct)).StatusCode);
        (await admin.PostAsJsonAsync(path, new { level = 1, reviewId = id }, Ct)).EnsureSuccessStatusCode();
    }
}
