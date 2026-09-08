using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class ManualAssignmentTests
{
    [Fact]
    public async Task AssignmentQueueAndCandidates_AreRestrictedToAuthorisedUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        SetUser(client, "admin");
        using var queue = await client.GetAsync(
            "/api/messages/grid?assignmentScope=assignable&skip=0&take=100&requireTotalCount=true", ct);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        using (var json = JsonDocument.Parse(await queue.Content.ReadAsStringAsync(ct)))
            Assert.Equal(75, json.RootElement.GetProperty("totalCount").GetInt32());

        var candidates = await client.GetFromJsonAsync<List<AssignmentCandidateDto>>(
            "/api/messages/1/assignment-candidates", ct);
        var candidate = Assert.Single(candidates!);
        Assert.Equal("amelia.hart", candidate.UserName);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/messages/1/assign", new AssignMessageRequest(5), ct)).StatusCode);

        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(
            "/api/messages/grid?assignmentScope=assignable&skip=0&take=20", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(
            "/api/messages/1/assignment-candidates", ct)).StatusCode);
    }

    [Fact]
    public async Task Reviews_RequireManualAssignmentAndReleaseItAfterEveryDecision()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        SetUser(client, "admin");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/assign", new AssignMessageRequest(3), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/start", new StartReviewRequest(1), ct)).StatusCode);

        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/start", new StartReviewRequest(1), ct)).StatusCode);

        SetUser(client, "admin");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/messages/3/reassign", new AssignMessageRequest(4), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/reject", new RejectReviewRequest(1, null), ct)).StatusCode);

        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/approve", new ApproveReviewRequest(1, null), ct)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var message = await db.Messages.SingleAsync(item => item.Id == 3, ct);
            Assert.Equal(MessageState.WaitingForSecondReview, message.State);
            Assert.Null(message.CurrentAssigneeId);
            Assert.NotNull((await db.Assignments.SingleAsync(item => item.MessageId == 3, ct)).EndedAt);
            Assert.Contains(await db.AuditEvents.Where(item => item.MessageId == 3).ToListAsync(ct),
                item => item.EventType == AuditEventType.MessageUnassigned);
        }

        SetUser(client, "admin");
        var candidates = await client.GetFromJsonAsync<List<AssignmentCandidateDto>>(
            "/api/messages/3/assignment-candidates", ct);
        Assert.Equal(["victor.stone"], candidates!.Select(candidate => candidate.UserName));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/assign", new AssignMessageRequest(4), ct)).StatusCode);

        SetUser(client, "victor.stone");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/start", new StartReviewRequest(2), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/reject", new RejectReviewRequest(2, null), ct)).StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var rejected = await verificationScope.ServiceProvider.GetRequiredService<ORPDbContext>()
            .Messages.SingleAsync(item => item.Id == 3, ct);
        Assert.Equal(MessageState.Rejected, rejected.State);
        Assert.Null(rejected.CurrentAssigneeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndoApproval_ReopensLevelWithoutAssignment(bool assignNextLevel)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var undoPermission = await db.Permissions.SingleAsync(item => item.Name == Permissions.ReviewUndo, ct);
            db.RolePermissions.Add(new RolePermission { RoleId = 3, PermissionId = undoPermission.Id });
            await db.SaveChangesAsync(ct);
        }

        SetUser(client, "admin");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/assign", new AssignMessageRequest(3), ct)).StatusCode);
        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/start", new StartReviewRequest(1), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/approve", new ApproveReviewRequest(1, null), ct)).StatusCode);

        if (assignNextLevel)
        {
            SetUser(client, "admin");
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
                "/api/messages/3/assign", new AssignMessageRequest(4), ct)).StatusCode);
        }

        long reviewId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            reviewId = (await db.Reviews.SingleAsync(item => item.MessageId == 3, ct)).Id;
        }
        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/undo", new UndoReviewRequest(reviewId), ct)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var message = await db.Messages.SingleAsync(item => item.Id == 3, ct);
            Assert.Equal(MessageState.Assigned, message.State);
            Assert.Null(message.CurrentAssigneeId);
            Assert.Equal(ReviewStatus.Undone, (await db.Reviews.SingleAsync(item => item.Id == reviewId, ct)).Status);
            var assignments = await db.Assignments.Where(item => item.MessageId == 3).ToListAsync(ct);
            Assert.Equal(assignNextLevel ? 2 : 1, assignments.Count);
            Assert.All(assignments, assignment => Assert.NotNull(assignment.EndedAt));
            var unassignments = await db.AuditEvents.Where(item => item.MessageId == 3 &&
                item.EventType == AuditEventType.MessageUnassigned).ToListAsync(ct);
            Assert.Equal(assignNextLevel ? 2 : 1, unassignments.Count);
        }

        SetUser(client, "admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/messages/3/assign", new AssignMessageRequest(4), ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/messages/3/assign", new AssignMessageRequest(3), ct)).StatusCode);
        SetUser(client, "priya.nair");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/messages/3/reviews/start", new StartReviewRequest(1), ct)).StatusCode);
    }

    private static void SetUser(HttpClient client, string userName)
    {
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", userName);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.UseSetting("UseMockData", "true");
            web.UseSetting("ConnectionStrings:ORP", "not-a-sql-server-connection");
        });
}
