using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Automatic;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class AutomaticAssignmentTests
{
    [Fact]
    public async Task Worker_AssignsNewMessagesAfterStartup()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory(workerEnabled: true, batchSize: 1);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", ct)).StatusCode);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var message = await scope.ServiceProvider.GetRequiredService<ORPDbContext>()
                .Messages.AsNoTracking().SingleAsync(item => item.Id == 1, ct);
            if (message.State == MessageState.Assigned)
            {
                Assert.NotNull(message.CurrentAssigneeId);
                return;
            }
            await Task.Delay(50, ct);
        }

        Assert.Fail("The automatic-assignment worker did not assign the first message.");
    }

    [Fact]
    public async Task Selector_UsesEligibilityLeastLoadAndStableTieBreak()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IAutomaticAssignmentQueries>();

        var firstPage = await queries.GetUnassignedMessagesAsync(null, 2, ct);
        Assert.Equal([1, 2], firstPage.Select(item => item.MessageId));
        var secondPage = await queries.GetUnassignedMessagesAsync(firstPage[^1], 2, ct);
        Assert.Equal([3, 4], secondPage.Select(item => item.MessageId));
        Assert.Equal(1, await queries.SelectAssigneeAsync(1, 1, 1, 1, [], ct));

        var otherMessage = await db.Messages.SingleAsync(message => message.Id == 4, ct);
        otherMessage.Assign(1);
        await db.SaveChangesAsync(ct);

        Assert.Equal(5, await queries.SelectAssigneeAsync(1, 1, 1, 1, [], ct));
        Assert.Equal(1, await queries.SelectAssigneeAsync(1, 1, 1, 1, [5], ct));
        Assert.Null(await queries.SelectAssigneeAsync(1, 1, 1, 1, [1, 5], ct));
    }

    [Fact]
    public async Task QueueCursor_DoesNotSkipRowsRemovedFromTheFirstPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IAutomaticAssignmentQueries>();

        var firstPage = await queries.GetUnassignedMessagesAsync(null, 2, ct);
        foreach (var message in await db.Messages.Where(message => message.Id <= 2).ToListAsync(ct))
            message.Assign(1);
        await db.SaveChangesAsync(ct);

        var secondPage = await queries.GetUnassignedMessagesAsync(firstPage[^1], 2, ct);
        Assert.Equal([3, 4], secondPage.Select(item => item.MessageId));
    }

    [Fact]
    public async Task ThreeLevelWorkflow_IsAutomaticallyAssignedAcrossThreeUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<AssignNewMessageHandler>()
                .HandleAsync(3, "auto-assignment-test", ct));
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            Assert.Equal(3, (await db.Messages.SingleAsync(message => message.Id == 3, ct)).CurrentAssigneeId);
            var assignment = await db.Assignments.SingleAsync(item => item.MessageId == 3 && item.EndedAt == null, ct);
            Assert.Null(assignment.AssignedBy);
            Assert.Null((await db.AuditEvents.OrderByDescending(item => item.Id)
                .FirstAsync(item => item.MessageId == 3 && item.EventType == AuditEventType.MessageAssigned, ct)).UserId);
        }

        await AssertAssignedToCurrentUser(client, "priya.nair", 3, ct);
        await CompleteLevel(client, "priya.nair", 3, 1, ct);
        await AssertAssignedToCurrentUser(client, "victor.stone", 3, ct);
        await CompleteLevel(client, "victor.stone", 3, 2, ct);
        await AssertAssignedToCurrentUser(client, "admin", 3, ct);
        await CompleteLevel(client, "admin", 3, 3, ct);

        await using var finalScope = factory.Services.CreateAsyncScope();
        var final = await finalScope.ServiceProvider.GetRequiredService<ORPDbContext>()
            .Messages.SingleAsync(message => message.Id == 3, ct);
        Assert.Equal(MessageState.Completed, final.State);
        Assert.Equal(5, final.CurrentAssigneeId);
    }

    [Fact]
    public async Task ApprovalWithoutNextReviewer_ReturnsConflictWithoutPersistingApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<AssignNewMessageHandler>()
                .HandleAsync(3, "auto-assignment-test", ct));
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var permissionId = await db.Permissions.Where(permission => permission.Name == Permissions.ReviewLevel2)
                .Select(permission => permission.Id).SingleAsync(ct);
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(grant => grant.PermissionId == permissionId));
            await db.SaveChangesAsync(ct);
        }

        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/start", new StartReviewRequest(1), ct)).StatusCode);
        using var response = await client.PostAsJsonAsync(
            "/api/messages/3/reviews/approve", new ApproveReviewRequest(1, null), ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("No eligible reviewer is available for review level 2.",
            problem.RootElement.GetProperty("detail").GetString());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ORPDbContext>();
        Assert.Equal(MessageState.FirstReviewInProgress,
            (await verificationDb.Messages.SingleAsync(message => message.Id == 3, ct)).State);
        Assert.Equal(ReviewStatus.InProgress,
            (await verificationDb.Reviews.SingleAsync(review => review.MessageId == 3, ct)).Status);
        Assert.DoesNotContain(await verificationDb.AuditEvents.Where(item => item.MessageId == 3).ToListAsync(ct),
            item => item.EventType == AuditEventType.ReviewApproved);
    }

    [Fact]
    public async Task Undo_AutomaticallyReassignsTheReopenedLevel()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var priyaRoleId = await db.UserRoles.Where(link => link.UserId == 3)
                .Select(link => link.RoleId).SingleAsync(ct);
            var undoPermissionId = await db.Permissions.Where(permission => permission.Name == Permissions.ReviewUndo)
                .Select(permission => permission.Id).SingleAsync(ct);
            db.RolePermissions.Add(new RolePermission { RoleId = priyaRoleId, PermissionId = undoPermissionId });
            await db.SaveChangesAsync(ct);
            Assert.True(await scope.ServiceProvider.GetRequiredService<AssignNewMessageHandler>()
                .HandleAsync(3, "auto-assignment-test", ct));
        }

        await CompleteLevel(client, "priya.nair", 3, 1, ct);
        long reviewId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            reviewId = await scope.ServiceProvider.GetRequiredService<ORPDbContext>().Reviews
                .Where(review => review.MessageId == 3 && review.Level == 1)
                .Select(review => review.Id).SingleAsync(ct);
        }
        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/undo", new UndoReviewRequest(reviewId), ct)).StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var message = await verificationScope.ServiceProvider.GetRequiredService<ORPDbContext>()
            .Messages.SingleAsync(item => item.Id == 3, ct);
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Equal(3, message.CurrentAssigneeId);
    }

    [Fact]
    public async Task ReassigningActiveReview_PreservesOwnerAndKeepsTheWorkDiscoverable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var message = await db.Messages.SingleAsync(item => item.Id == 1, ct);
            message.Assign(5);
            db.Assignments.Add(new Assignment(message.Id, null, 5, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }

        SetUser(client, "admin");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/messages/1/reviews/start", new StartReviewRequest(1), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/1/reassign", new AssignMessageRequest(1), ct)).StatusCode);

        var reviewerRow = await FindMineRow(client, 1, ct);
        Assert.Equal(1, reviewerRow.GetProperty("currentAssigneeId").GetInt32());
        Assert.Equal(1, reviewerRow.GetProperty("activeReviewLevel").GetInt32());
        Assert.Equal(5, reviewerRow.GetProperty("activeReviewerId").GetInt32());

        SetUser(client, "amelia.hart");
        var assigneeRow = await FindMineRow(client, 1, ct);
        Assert.Equal(5, assigneeRow.GetProperty("activeReviewerId").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/messages/1/reviews/approve", new ApproveReviewRequest(1, null), ct)).StatusCode);

        SetUser(client, "admin");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/1/reviews/approve", new ApproveReviewRequest(1, null), ct)).StatusCode);
    }

    private static async Task CompleteLevel(HttpClient client, string userName, long messageId,
        int level, CancellationToken ct)
    {
        SetUser(client, userName);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            $"/api/messages/{messageId}/reviews/start", new StartReviewRequest(level), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            $"/api/messages/{messageId}/reviews/approve", new ApproveReviewRequest(level, null), ct)).StatusCode);
    }

    private static async Task AssertAssignedToCurrentUser(HttpClient client, string userName,
        long messageId, CancellationToken ct)
    {
        SetUser(client, userName);
        using var response = await client.GetAsync(
            "/api/messages/grid?assignmentScope=mine&skip=0&take=100", ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Contains(json.RootElement.GetProperty("data").EnumerateArray(),
            row => row.GetProperty("id").GetInt64() == messageId);
    }

    private static async Task<JsonElement> FindMineRow(HttpClient client, long messageId,
        CancellationToken ct)
    {
        using var response = await client.GetAsync(
            "/api/messages/grid?assignmentScope=mine&skip=0&take=100", ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("data").EnumerateArray()
            .Single(row => row.GetProperty("id").GetInt64() == messageId)
            .Clone();
    }

    private static void SetUser(HttpClient client, string userName)
    {
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", userName);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool workerEnabled = false,
        int batchSize = 100) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.UseSetting("UseMockData", "true");
            web.UseSetting("AutoAssignment:Enabled", workerEnabled.ToString());
            web.UseSetting("AutoAssignment:IntervalSeconds", "1");
            web.UseSetting("AutoAssignment:BatchSize", batchSize.ToString());
            web.UseSetting("ConnectionStrings:ORP", "not-a-sql-server-connection");
        });
}
