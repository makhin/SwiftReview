using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class MessageWorkflowApiTests : IDisposable
{
    private readonly MessageApiFactory factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public void Dispose() => factory.Dispose();
    private HttpClient Client(string user = "admin")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", user);
        return client;
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("cancel", true)]
    [InlineData("undo", true)]
    [InlineData("active", false)]
    [InlineData("approve", false)]
    [InlineData("reject", false)]
    public async Task ChangeWorkflow_RespectsReviewHistory_AndExplainsRefusal(string action, bool allowed)
    {
        using var client = Client();
        (await client.PutAsJsonAsync("/api/admin/users/5/access", new { assignments = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 5 }, Ct)).EnsureSuccessStatusCode();
        if (action != "none")
        {
            var start = await client.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
            start.EnsureSuccessStatusCode();
            var reviewId = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
            if (action != "active")
                (await client.PostAsJsonAsync($"/api/messages/1/reviews/{(action == "undo" ? "approve" : action)}", new { reviewId = await factory.LatestReviewIdAsync(1), level = 1 }, Ct)).EnsureSuccessStatusCode();
            if (action == "undo")
                (await client.PostAsJsonAsync("/api/messages/1/undo", new { reviewId }, Ct)).EnsureSuccessStatusCode();
        }
        var grid = await client.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=100", Ct);
        Assert.Equal(allowed, grid.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetInt64() == 1).GetProperty("canChangeWorkflow").GetBoolean());
        var response = await client.PutAsJsonAsync("/api/messages/1/workflow", new { workflowDefinitionId = 3 }, Ct);
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var message = await db.Messages.SingleAsync(m => m.Id == 1, Ct);
        Assert.Equal(allowed ? 3 : 1, message.WorkflowDefinitionId);
        Assert.Equal(action == "none" ? 0 : 1, await db.Reviews.CountAsync(r => r.MessageId == 1, Ct));
        Assert.Equal(allowed ? 1 : 0, await db.AuditEvents.CountAsync(e => e.MessageId == 1 && e.EventType == AuditEventType.MessageWorkflowChanged, Ct));
        if (allowed)
        {
            (await client.PutAsJsonAsync("/api/messages/1/workflow", new { workflowDefinitionId = 3 }, Ct)).EnsureSuccessStatusCode();
            Assert.Equal(1, await db.AuditEvents.CountAsync(e => e.MessageId == 1 && e.EventType == AuditEventType.MessageWorkflowChanged, Ct));
            (await client.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        }
        else
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Contains("cancelled or undone", problem.GetProperty("detail").GetString());
        }
    }

    [Fact]
    public async Task CancelledLaterAttempt_DoesNotHideEarlierApproval()
    {
        using var client = Client();
        (await client.PostAsJsonAsync("/api/messages/3/assign", new { assignedTo = 5 }, Ct)).EnsureSuccessStatusCode();
        var start = await client.PostAsJsonAsync("/api/messages/3/reviews/start", new { level = 1 }, Ct);
        var reviewId = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
        (await client.PostAsJsonAsync("/api/messages/3/reviews/approve", new { reviewId = await factory.LatestReviewIdAsync(3), level = 1 }, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/messages/3/reviews/start", new { level = 2 }, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/messages/3/reviews/cancel", new { reviewId = await factory.LatestReviewIdAsync(3), level = 2 }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/messages/3/workflow", new { workflowDefinitionId = 1 }, Ct)).StatusCode);
        (await client.PostAsJsonAsync("/api/messages/3/undo", new { reviewId }, Ct)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/messages/3/workflow", new { workflowDefinitionId = 1 }, Ct)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ScopedPermission_AllowsChangeWithoutAssign_ButRejectsOtherScopesAndInactiveWorkflow()
    {
        using var admin = Client();
        using var user = Client("amelia.hart");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var workflow = new WorkflowDefinition("Manual two levels", "MT999", 1).AddStep(1, 1).AddStep(2, 2);
        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync(Ct);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync("/api/messages/1/workflow", new { workflowDefinitionId = workflow.Id }, Ct)).StatusCode);
        (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { "message.view", "workflow.manage" } }, Ct)).EnsureSuccessStatusCode();
        (await user.PutAsJsonAsync("/api/messages/1/workflow", new { workflowDefinitionId = workflow.Id }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync("/api/messages/1/workflow", new { workflowDefinitionId = 3 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync("/api/messages/3/workflow", new { workflowDefinitionId = workflow.Id }, Ct)).StatusCode);
        workflow.Deactivate();
        await db.SaveChangesAsync(Ct);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/messages/1/workflow", new { workflowDefinitionId = workflow.Id }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/messages/1/workflow", new { workflowDefinitionId = 0 }, Ct)).StatusCode);
    }
}
