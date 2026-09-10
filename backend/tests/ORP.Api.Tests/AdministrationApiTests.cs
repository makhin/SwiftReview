using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Application.Administration;
using ORP.Domain.Identity;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class AdministrationApiTests : IDisposable
{
    private readonly MessageApiFactory factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private HttpClient Client(string? user = "admin")
    {
        var client = factory.CreateClient();
        if (user is not null) client.DefaultRequestHeaders.Add("X-Debug-User", user);
        return client;
    }
    public void Dispose() => factory.Dispose();

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("amelia.hart", HttpStatusCode.Forbidden)]
    public async Task Administration_RejectsNonAdministrators(string? user, HttpStatusCode expected)
    {
        using var client = Client(user);
        foreach (var path in new[] { "/api/admin/catalog", "/api/admin/users/grid", "/api/admin/users/1/access" })
            Assert.Equal(expected, (await client.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(expected, (await client.PutAsJsonAsync("/api/admin/users/1/access", new { assignments = Array.Empty<object>() }, Ct)).StatusCode);
        Assert.Equal(expected, (await client.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = Array.Empty<string>() }, Ct)).StatusCode);
    }

    [Fact]
    public async Task GlobalAdminWithoutBusinessRoles_CanManageButCannotReadMessages()
    {
        using var admin = Client();
        (await admin.PutAsJsonAsync("/api/admin/users/5/access", new { assignments = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
        (await admin.GetAsync("/api/admin/catalog", Ct)).EnsureSuccessStatusCode();
        var grid = await admin.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=20", Ct);
        Assert.Empty(grid.GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/messages/1", Ct)).StatusCode);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/me", Ct);
        Assert.True(me.GetProperty("isGlobalAdministrator").GetBoolean());
        Assert.Equal(0, me.GetProperty("scopes").GetArrayLength());
    }

    [Fact]
    public async Task Assignments_AreAudited_AndDoNotCreateCrossProductAccess()
    {
        using var admin = Client();
        var request = new UpdateUserAccessRequest([
            new(1, 1, [1, 2]), new(2, 2, [1])]);
        (await admin.PutAsJsonAsync("/api/admin/users/1/access", request, Ct)).EnsureSuccessStatusCode();
        using var user = Client("amelia.hart");
        var me = await user.GetFromJsonAsync<JsonElement>("/api/me", Ct);
        Assert.Equal(2, me.GetProperty("scopes").GetArrayLength());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var access = await scope.ServiceProvider.GetRequiredService<IUserAccessService>().GetByIdAsync(1, Ct);
        Assert.True(access!.HasPermission(Permissions.ReviewLevel2, 1, 1));
        Assert.False(access.HasPermission(Permissions.ReviewLevel2, 2, 2));
        Assert.False(access.CanAccess(1, 2));
        var denied = await db.SwiftMessages.FirstAsync(m => m.BranchId == 1 && m.DepartmentId == 2, Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/messages/{denied.MessageId}", Ct)).StatusCode);
        var grid = await user.GetFromJsonAsync<JsonElement>("/api/messages/grid?skip=0&take=100&requireTotalCount=true", Ct);
        var expectedIds = await db.SwiftMessages.Where(m => (m.BranchId == 1 && m.DepartmentId == 1) || (m.BranchId == 2 && m.DepartmentId == 2)).Select(m => m.MessageId).ToArrayAsync(Ct);
        Assert.Equal(expectedIds.Order(), grid.GetProperty("data").EnumerateArray().Select(m => m.GetProperty("id").GetInt64()).Order());
        var audit = await db.AccessAuditEvents.SingleAsync(Ct);
        Assert.Equal(5, audit.ActorId);
        Assert.Equal(1, audit.TargetUserId);
        Assert.Contains("roleIds", audit.AfterJson);
        Assert.NotEqual(audit.BeforeJson, audit.AfterJson);
        Assert.False(string.IsNullOrWhiteSpace(audit.CorrelationId));
    }

    [Fact]
    public async Task RoleEdit_UpdatesAllAssignedUsers_WithoutGrantingAdministrator()
    {
        using var admin = Client();
        (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { Permissions.MessageView, Permissions.AuditView } }, Ct)).EnsureSuccessStatusCode();
        foreach (var name in new[] { "amelia.hart", "lucas.bennett" })
        {
            using var user = Client(name);
            var me = await user.GetFromJsonAsync<JsonElement>("/api/me", Ct);
            Assert.Contains(me.GetProperty("permissions").EnumerateArray(), p => p.GetString() == Permissions.AuditView);
            Assert.DoesNotContain(me.GetProperty("permissions").EnumerateArray(), p => p.GetString() == Permissions.ReviewLevel1);
            Assert.False(me.GetProperty("isGlobalAdministrator").GetBoolean());
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/admin/catalog", Ct)).StatusCode);
        }
        using var scope = factory.Services.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<ORPDbContext>().AccessAuditEvents.SingleAsync(Ct);
        Assert.Equal(1, audit.RoleId);
        Assert.Null(audit.TargetUserId);
    }

    [Fact]
    public async Task InvalidAssignmentsAndPrivilegeInjection_DoNotMutateOrAudit()
    {
        using var admin = Client();
        var badBodies = new object[] {
            new { assignments = new[] { new { branchId = 999, departmentId = 1, roleIds = new[] { 1 } } } },
            new { assignments = new[] { new { branchId = 1, departmentId = 1, roleIds = new[] { 1, 1 } } } },
            new { assignments = new[] { new { branchId = 1, departmentId = 1, roleIds = Array.Empty<int>() } } },
            new { assignments = Array.Empty<object>(), isGlobalAdministrator = true },
            new { assignments = Array.Empty<object>(), permissions = new[] { Permissions.MessageView } }
        };
        foreach (var body in badBodies)
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/users/1/access", body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { "global_admin" } }, Ct)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        Assert.Empty(await db.AccessAuditEvents.ToListAsync(Ct));
        Assert.False((await db.Users.SingleAsync(u => u.Id == 1, Ct)).IsGlobalAdministrator);
        Assert.Single(await db.UserRoles.Where(r => r.UserId == 1).ToListAsync(Ct));
    }

    [Fact]
    public async Task StartingReviewAgain_ResumesSameReview_AndKeepsAssignmentLocked()
    {
        using var admin = Client();
        using var reviewer = Client("amelia.hart");
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        var first = await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
        first.EnsureSuccessStatusCode();
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var resumed = await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct);
        resumed.EnsureSuccessStatusCode();
        var resumedBody = await resumed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(firstBody.GetProperty("reviewId").GetInt64(), resumedBody.GetProperty("reviewId").GetInt64());

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 2 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 2 }, Ct)).StatusCode);
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/approve", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        Assert.Single(await db.Reviews.Where(r => r.MessageId == 1).ToListAsync(Ct));
        Assert.Single(await db.AuditEvents.Where(e => e.MessageId == 1 && e.EventType == AuditEventType.ReviewStarted).ToListAsync(Ct));
    }

    [Fact]
    public async Task CancelReview_IsOwnerOnly_KeepsHistoryAndAssignment_AndUnlocksReassignment()
    {
        using var admin = Client();
        using var reviewer = Client("amelia.hart");
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/messages/1/reviews/cancel", new { level = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/cancel", new { level = 2 }, Ct)).StatusCode);

        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/cancel", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/cancel", new { level = 1 }, Ct)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var message = await db.Messages.SingleAsync(m => m.Id == 1, Ct);
            Assert.Equal(MessageState.Assigned, message.State);
            Assert.Equal(1, message.CurrentAssigneeId);
            var review = await db.Reviews.SingleAsync(r => r.MessageId == 1, Ct);
            Assert.Equal(ReviewStatus.Cancelled, review.Status);
            Assert.NotNull(review.CompletedAt);
            Assert.Single(await db.Assignments.Where(a => a.MessageId == 1 && a.EndedAt == null).ToListAsync(Ct));
            var audit = await db.AuditEvents.SingleAsync(e => e.MessageId == 1 && e.EventType == AuditEventType.ReviewCancelled, Ct);
            Assert.Equal(review.Id, audit.ReviewId);
            Assert.Equal(1, audit.UserId);
            Assert.Equal(MessageState.FirstReviewInProgress, audit.OldState);
            Assert.Equal(MessageState.Assigned, audit.NewState);
        }
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/cancel", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/users/6/access", new UpdateUserAccessRequest([new(1, 1, [1])]), Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/messages/1/reassign", new { assignedTo = 6 }, Ct)).EnsureSuccessStatusCode();
        using var nextReviewer = Client("lucas.bennett");
        (await nextReviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        (await nextReviewer.PostAsJsonAsync("/api/messages/1/reviews/approve", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await nextReviewer.PostAsJsonAsync("/api/messages/1/reviews/cancel", new { level = 1 }, Ct)).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ORPDbContext>();
        Assert.Equal(3, await finalDb.Reviews.CountAsync(r => r.MessageId == 1, Ct));
        Assert.Equal(2, await finalDb.AuditEvents.CountAsync(e => e.MessageId == 1 && e.EventType == AuditEventType.ReviewCancelled, Ct));
    }

    [Fact]
    public async Task ActiveReview_BlocksRevocationOfUserAccessAndRolePermissions()
    {
        using var admin = Client();
        using var reviewer = Client("amelia.hart");
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/admin/users/1/access", new { assignments = Array.Empty<object>() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { Permissions.MessageView } }, Ct)).StatusCode);
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/approve", new { level = 1, comment = "Reviewed" }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/users/1/access", new { assignments = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<ORPDbContext>().AccessAuditEvents.ToListAsync(Ct));
    }

    [Fact]
    public async Task ScopedActionPermissions_DoNotLeakToOtherScopes()
    {
        using var admin = Client();
        (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { Permissions.MessageView, Permissions.ReviewLevel1 } }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/users/1/access", new UpdateUserAccessRequest([new(1, 1, [1]), new(2, 2, [5])]), Ct)).EnsureSuccessStatusCode();
        using var user = Client("amelia.hart");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync("/api/messages/1/audit", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/messages/1/assignment-candidates", Ct)).StatusCode);
    }

    [Fact]
    public async Task AdminUserGrid_SupportsSearchPagingAndValidatesSort()
    {
        using var admin = Client();
        var result = await admin.GetFromJsonAsync<JsonElement>("/api/admin/users/grid?search=amelia&take=1", Ct);
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
        Assert.Equal("amelia.hart", result.GetProperty("data")[0].GetProperty("userName").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/users/grid?take=999", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/users/grid?sort=%5B%7B%22selector%22:%22isGlobalAdministrator%22%7D%5D", Ct)).StatusCode);
    }

    [Fact]
    public async Task SeparateRoles_CombineOnlyWithinOnePair_ForReviewEligibility()
    {
        using var admin = Client();
        (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { Permissions.MessageView } }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/roles/2/permissions", new { permissions = new[] { Permissions.ReviewLevel1 } }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/admin/users/1/access", new UpdateUserAccessRequest([new(1, 1, [1]), new(2, 2, [2])]), Ct)).EnsureSuccessStatusCode();
        var excluded = await admin.GetFromJsonAsync<JsonElement>("/api/messages/1/assignment-candidates", Ct);
        Assert.DoesNotContain(excluded.EnumerateArray(), u => u.GetProperty("id").GetInt32() == 1);
        (await admin.PutAsJsonAsync("/api/admin/users/1/access", new UpdateUserAccessRequest([new(1, 1, [1, 2])]), Ct)).EnsureSuccessStatusCode();
        var included = await admin.GetFromJsonAsync<JsonElement>("/api/messages/1/assignment-candidates", Ct);
        Assert.Contains(included.EnumerateArray(), u => u.GetProperty("id").GetInt32() == 1);
        (await admin.PostAsJsonAsync("/api/messages/1/assign", new { assignedTo = 1 }, Ct)).EnsureSuccessStatusCode();
        using var reviewer = Client("amelia.hart");
        (await reviewer.PostAsJsonAsync("/api/messages/1/reviews/start", new { level = 1 }, Ct)).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(1, "approve")]
    [InlineData(2, "approve")]
    [InlineData(3, "approve")]
    [InlineData(1, "reject")]
    [InlineData(2, "reject")]
    [InlineData(3, "reject")]
    public async Task ReviewDecisions_RequireCurrentLevelAndActiveOwner(int level, string decision)
    {
        using var admin = Client();
        using var reviewer = Client("amelia.hart");
        var wrongPermission = level == 1 ? Permissions.ReviewLevel2 : Permissions.ReviewLevel1;
        (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { Permissions.MessageView, wrongPermission } }, Ct)).EnsureSuccessStatusCode();
        // Set up an active review at each level, independently of earlier workflow stages.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var message = await db.Messages.SingleAsync(m => m.Id == 1, Ct);
            var workflow = new ORP.Domain.Workflows.WorkflowDefinition("Three levels", "MT199", 1);
            for (var step = 1; step <= 3; step++) workflow.AddStep(step, step);
            db.WorkflowDefinitions.Add(workflow);
            await db.SaveChangesAsync(Ct);
            db.Entry(message).Property(m => m.WorkflowDefinitionId).CurrentValue = workflow.Id;
            db.Entry(message).Property(m => m.CurrentAssigneeId).CurrentValue = 1;
            db.Entry(message).Property(m => m.State).CurrentValue = level switch
            {
                1 => ORP.Domain.Messages.MessageState.FirstReviewInProgress,
                2 => ORP.Domain.Messages.MessageState.SecondReviewInProgress,
                _ => ORP.Domain.Messages.MessageState.ThirdReviewInProgress
            };
            db.Assignments.Add(new ORP.Domain.Assignments.Assignment(1, 5, 1, DateTimeOffset.UtcNow));
            for (var previousLevel = 1; previousLevel < level; previousLevel++)
            {
                var previous = new ORP.Domain.Reviews.Review(1, previousLevel, previousLevel + 1, DateTimeOffset.UtcNow);
                previous.Approve("Previous stage", DateTimeOffset.UtcNow);
                db.Reviews.Add(previous);
            }
            db.Reviews.Add(new ORP.Domain.Reviews.Review(1, level, 1, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(Ct);
        }
        var path = $"/api/messages/1/reviews/{decision}";
        var body = new { level, comment = "Decision" };
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync(path, body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(path, body, Ct)).StatusCode);
        (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions", new { permissions = new[] { Permissions.MessageView, $"review.level{level}" } }, Ct)).EnsureSuccessStatusCode();
        (await reviewer.PostAsJsonAsync(path, body, Ct)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RemovedRejectPermission_IsNotInCatalogAndCannotBeGranted()
    {
        using var admin = Client();
        var catalog = await admin.GetFromJsonAsync<AccessCatalogDto>("/api/admin/catalog", Ct);
        Assert.DoesNotContain("review.reject", catalog!.Permissions);
        Assert.All(catalog.Roles, role => Assert.DoesNotContain("review.reject", role.Permissions));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/roles/1/permissions",
            new { permissions = new[] { "review.reject" } }, Ct)).StatusCode);
    }

    [Fact]
    public async Task EveryStartingRole_HasAuditAccess_WithinMessageScopeOnly()
    {
        using var admin = Client();
        var catalog = await admin.GetFromJsonAsync<AccessCatalogDto>("/api/admin/catalog", Ct);
        Assert.NotEmpty(catalog!.Roles);
        Assert.All(catalog.Roles, role => Assert.Contains(Permissions.AuditView, role.Permissions));
        using var reviewer = Client("amelia.hart");
        (await reviewer.GetAsync("/api/messages/1/audit", Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync("/api/messages/2/audit", Ct)).StatusCode);
    }
}
