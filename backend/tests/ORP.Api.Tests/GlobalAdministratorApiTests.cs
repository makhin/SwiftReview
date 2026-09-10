using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class GlobalAdministratorApiTests : IDisposable
{
    private readonly MessageApiFactory factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private HttpClient Client(string user = "admin")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", user);
        return client;
    }
    private async Task<HttpClient> AdministratorWithoutRoles()
    {
        var admin = Client();
        (await admin.PutAsJsonAsync("/api/admin/users/5/access", new { assignments = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
        return admin;
    }
    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task WithoutRoles_SeesEveryScopeReferenceAndAudit()
    {
        using var admin = await AdministratorWithoutRoles();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var total = await db.Messages.CountAsync(Ct);
        foreach (var path in new[] { "/api/messages/grid?skip=0&take=100&requireTotalCount=true", "/api/messages/grid?assignmentScope=departments&skip=0&take=100&requireTotalCount=true" })
        {
            var grid = await admin.GetFromJsonAsync<JsonElement>(path, Ct);
            Assert.Equal(total, grid.GetProperty("totalCount").GetInt32());
        }
        var searchResponse = await admin.PostAsJsonAsync("/api/messages/search", new { skip = 0, take = 100 }, Ct);
        searchResponse.EnsureSuccessStatusCode();
        var search = await searchResponse.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(total, search.GetProperty("totalCount").GetInt32());
        var dashboard = await admin.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary", Ct);
        Assert.Equal(total, dashboard!.Total);
        foreach (var id in await db.Messages.Select(m => m.Id).Take(3).ToArrayAsync(Ct))
        {
            (await admin.GetAsync($"/api/messages/{id}", Ct)).EnsureSuccessStatusCode();
            (await admin.GetAsync($"/api/messages/{id}/audit", Ct)).EnsureSuccessStatusCode();
        }
        Assert.Equal(await db.Users.CountAsync(Ct), (await admin.GetFromJsonAsync<UserSummaryDto[]>("/api/users", Ct))!.Length);
        Assert.Equal(await db.Branches.CountAsync(Ct), (await admin.GetFromJsonAsync<ReferenceItemDto[]>("/api/branches", Ct))!.Length);
        Assert.Equal(await db.Departments.CountAsync(Ct), (await admin.GetFromJsonAsync<ReferenceItemDto[]>("/api/departments", Ct))!.Length);
        Assert.Equal(await db.WorkflowDefinitions.CountAsync(Ct), (await admin.GetFromJsonAsync<WorkflowSummaryDto[]>("/api/workflows", Ct))!.Length);
        (await admin.GetAsync("/api/message-types", Ct)).EnsureSuccessStatusCode();
        (await admin.GetAsync("/api/message-states", Ct)).EnsureSuccessStatusCode();
        var candidates = await admin.GetFromJsonAsync<AssignmentCandidateDto[]>("/api/messages/1/assignment-candidates", Ct);
        Assert.Contains(candidates!, c => c.Id == 5);
        var me = await admin.GetFromJsonAsync<CurrentUserResponse>("/api/me", Ct);
        Assert.True(me!.IsGlobalAdministrator);
        Assert.Empty(me.Scopes);
        Assert.Contains("review.level3", me.Permissions);
    }

    [Theory]
    [InlineData("approve", AuditEventType.ReviewApproved)]
    [InlineData("reject", AuditEventType.ReviewRejected)]
    [InlineData("cancel", AuditEventType.ReviewCancelled)]
    public async Task CanActOnAnotherUsersActiveReview_AndAuditRecordsActualActor(string action, AuditEventType eventType)
    {
        using var admin = await AdministratorWithoutRoles();
        using var reviewer = Client("amelia.hart");
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        var started = await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
        var reviewId = (await started.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
        var resumed = await admin.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
        resumed.EnsureSuccessStatusCode();
        Assert.Equal(reviewId, (await resumed.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId);
        (await admin.PostAsJsonAsync($"/api/messages/1/reviews/{action}", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var audit = await db.AuditEvents.SingleAsync(e => e.MessageId == 1 && e.EventType == eventType, Ct);
        Assert.Equal(5, audit.UserId);
        Assert.Equal(reviewId, audit.ReviewId);
        Assert.Equal(1, (await db.Reviews.SingleAsync(r => r.Id == reviewId, Ct)).ReviewerId);
        if (action == "approve")
        {
            (await admin.PostAsJsonAsync("/api/messages/1/undo", new { reviewId }, Ct)).EnsureSuccessStatusCode();
            Assert.Equal(5, (await db.AuditEvents.SingleAsync(e => e.MessageId == 1 && e.EventType == AuditEventType.ConfirmationUndone, Ct)).UserId);
        }
        if (action == "cancel")
        {
            (await admin.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
            Assert.Equal(5, (await db.Messages.AsNoTracking().SingleAsync(m => m.Id == 1, Ct)).CurrentAssigneeId);
            Assert.Equal(5, (await db.Reviews.AsNoTracking().SingleAsync(r => r.MessageId == 1 && r.Status == ORP.Domain.Reviews.ReviewStatus.InProgress, Ct)).ReviewerId);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Recheck the details  ", "Recheck the details")]
    public async Task UndoComment_IsOptionalValidatedAndAudited_WithoutOverwritingApprovalComment(string? comment, string? expected)
    {
        using var admin = await AdministratorWithoutRoles();
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 5 }, Ct)).EnsureSuccessStatusCode();
        var start = await admin.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
        var reviewId = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
        (await admin.PostAsJsonAsync("/api/messages/1/reviews/approve", new { level = 1, comment = "Original approval" }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/messages/1/undo", new { reviewId, comment = new string('x', 2001) }, Ct)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            Assert.Equal(MessageState.Completed, (await db.Messages.SingleAsync(m => m.Id == 1, Ct)).State);
            Assert.False(await db.AuditEvents.AnyAsync(e => e.MessageId == 1 && e.EventType == AuditEventType.ConfirmationUndone, Ct));
        }
        (await admin.PostAsJsonAsync("/api/messages/1/undo", new { reviewId, comment }, Ct)).EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var trail = await admin.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/messages/1/audit", options, Ct);
        Assert.Equal(expected, trail!.Items.Single(e => e.EventType == AuditEventType.ConfirmationUndone).Details.Comment);
        Assert.Equal("Original approval", trail.Items.Single(e => e.EventType == AuditEventType.ReviewApproved).Details.Comment);
        using var finalScope = factory.Services.CreateScope();
        Assert.Equal("Original approval", (await finalScope.ServiceProvider.GetRequiredService<ORPDbContext>().Reviews.SingleAsync(r => r.Id == reviewId, Ct)).Comment);
    }

    [Fact]
    public async Task UndoGridAction_UsesLatestApproval_AndRequiresGlobalAdministrator()
    {
        using var admin = await AdministratorWithoutRoles();
        using var reviewer = Client("amelia.hart");
        (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { "message.view", "review.level1", "review.undo" } }, Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        var started = await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
        var reviewId = (await started.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/approve", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        const string path = "/api/messages/grid?skip=0&take=100";
        var grid = await admin.GetFromJsonAsync<JsonElement>(path, Ct);
        var row = grid.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetInt64() == 1);
        Assert.Equal(reviewId, row.GetProperty("undoReviewId").GetInt64());
        var ordinaryGrid = await reviewer.GetFromJsonAsync<JsonElement>(path, Ct);
        Assert.Equal(JsonValueKind.Null, ordinaryGrid.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetInt64() == 1).GetProperty("undoReviewId").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync("/api/messages/1/undo", new { reviewId }, Ct)).StatusCode);
        (await admin.PostAsJsonAsync("/api/messages/1/undo", new { reviewId }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/messages/1/undo", new { reviewId }, Ct)).StatusCode);
        var refreshed = await admin.GetFromJsonAsync<JsonElement>(path, Ct);
        var updated = refreshed.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetInt64() == 1);
        Assert.Equal("Assigned", updated.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("currentAssigneeId").ValueKind);
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("undoReviewId").ValueKind);
    }

    [Fact]
    public async Task UndoGridAction_IsUnavailableDuringReview_AndReopensLatestApprovedLevel()
    {
        using var admin = await AdministratorWithoutRoles();
        const long id = 3;
        async Task<JsonElement> Row()
        {
            var grid = await admin.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=100", Ct);
            return grid.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetInt64() == id);
        }
        (await admin.PostAsJsonAsync($"/api/messages/{id}/assign", new { assignedTo = 5 }, Ct)).EnsureSuccessStatusCode();
        long latestId = 0;
        for (var level = 1; level <= 2; level++)
        {
            var start = await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new { level }, Ct);
            latestId = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
            Assert.Equal(JsonValueKind.Null, (await Row()).GetProperty("undoReviewId").ValueKind);
            (await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/approve", new { level }, Ct)).EnsureSuccessStatusCode();
            Assert.Equal(latestId, (await Row()).GetProperty("undoReviewId").GetInt64());
        }
        (await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new { level = 3 }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/messages/{id}/undo", new { reviewId = latestId }, Ct)).StatusCode);
        (await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/cancel", new { level = 3 }, Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/messages/{id}/undo", new { reviewId = latestId }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal("WaitingForSecondReview", (await Row()).GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, (await Row()).GetProperty("currentAssigneeId").ValueKind);
    }

    [Fact]
    public async Task CanSelfAssignAndReviewAllLevels_ButCannotBypassWorkflowStates()
    {
        using var admin = await AdministratorWithoutRoles();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var id = await db.Messages.Where(m => db.WorkflowSteps.Count(s => s.WorkflowDefinitionId == m.WorkflowDefinitionId && s.Required) == 3)
            .Select(m => m.Id).FirstAsync(Ct);
        (await admin.PostAsJsonAsync($"/api/messages/{id}/assign", new { assignedTo = 5 }, Ct)).EnsureSuccessStatusCode();
        for (var level = 1; level <= 3; level++)
        {
            (await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new { level }, Ct)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/messages/{id}/reassign", new { assignedTo = 1 }, Ct)).StatusCode);
            (await admin.PutAsJsonAsync("/api/admin/users/5/access", new { assignments = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
            (await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/approve", new { level }, Ct)).EnsureSuccessStatusCode();
        }
        Assert.Equal(MessageState.Completed, (await db.Messages.AsNoTracking().SingleAsync(m => m.Id == id, Ct)).State);
        Assert.False((await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new { level = 1 }, Ct)).IsSuccessStatusCode);
        Assert.False((await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/approve", new { level = 3 }, Ct)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new { level = 0 }, Ct)).StatusCode);
    }
}
