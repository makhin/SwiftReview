using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Assignments.Assign;
using ORP.Application.Authorization;
using ORP.Application.Grids;
using ORP.Application.Messages.Get;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Logging;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed partial class ConfirmationSqlTests
{
    private static readonly JsonSerializerOptions LevelJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    [Theory]
    [InlineData(ConfirmationMode.Immediate, AssignmentRetentionMode.PerConfirmation, false)]
    [InlineData(ConfirmationMode.Immediate, AssignmentRetentionMode.UntilWorkflowCompleted, true)]
    [InlineData(ConfirmationMode.ReviewSession, AssignmentRetentionMode.PerConfirmation, false)]
    [InlineData(ConfirmationMode.ReviewSession, AssignmentRetentionMode.UntilWorkflowCompleted, false)]
    public async Task FutureAssignmentsAppearInMineAndActivateSequentially(ConfirmationMode mode,
        AssignmentRetentionMode retention, bool skipSecond)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await ConfigureLevels(fixture, setup.Message, mode, retention, skipSecond, ct);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        SetActor(client, "theo.mercer");
        var initial = (await client.GetFromJsonAsync<MessageDetailsDto>($"/api/messages/{setup.Message}", LevelJsonOptions, ct))!;
        // Reserve the last level before even assigning the first one.
        await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Assignee, level = 3 }, ct);
        var reserved = (await client.GetFromJsonAsync<MessageDetailsDto>($"/api/messages/{setup.Message}", LevelJsonOptions, ct))!;
        Assert.Equal(MessageState.New, reserved.State);
        Assert.Null(reserved.CurrentAssigneeId);
        Assert.NotEqual(initial.Version, reserved.Version);
        Assert.Equal(3, Assert.Single(reserved.Assignments).Level);
        var view = (await client.GetFromJsonAsync<MessageViewDto>($"/api/messages/{setup.Message}/view", LevelJsonOptions, ct))!;
        Assert.Equal("Priya Nair", view.Approvals.Single(a => a.Level == 3).Assignee);
        SetActor(client, "priya.nair");
        var gridResponse = await client.PostAsJsonAsync("/api/messages/grid",
            new MessageGridRequest { AssignmentScope = "mine" }, ct);
        gridResponse.EnsureSuccessStatusCode();
        var grid = (await gridResponse.Content.ReadFromJsonAsync<PagedResult<MessageGridRowDto>>(LevelJsonOptions, ct))!;
        Assert.Contains(grid.Items, m => m.Id == setup.Message && !m.CanConfirm && !m.CanReview);
        var counts = (await client.GetFromJsonAsync<MessageStateCountsDto>("/api/messages/state-counts?assignmentScope=mine", LevelJsonOptions, ct))!;
        Assert.Equal(grid.TotalCount, counts.Total.Count);
        var early = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/confirm", new { level = 3 }, ct);
        Assert.False(early.IsSuccessStatusCode);
        SetActor(client, "theo.mercer");
        // Omitted level remains backward compatible.
        await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Actor }, ct);
        if (!skipSecond)
            await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Actor, level = 2 }, ct);
        foreach (var level in skipSecond ? new[] { 1, 3 } : new[] { 1, 2, 3 })
        {
            SetActor(client, level == 3 ? "priya.nair" : "theo.mercer");
            var details = (await client.GetFromJsonAsync<MessageDetailsDto>($"/api/messages/{setup.Message}", LevelJsonOptions, ct))!;
            Assert.Equal(level == 3 ? setup.Assignee : setup.Actor, details.CurrentAssigneeId);
            if (mode == ConfirmationMode.Immediate)
                await PostOk(client, $"/api/messages/{setup.Message}/reviews/confirm", new { level }, ct);
            else
            {
                var start = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/start", new { level }, ct);
                start.EnsureSuccessStatusCode();
                var review = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(ct))!;
                await PostOk(client, $"/api/messages/{setup.Message}/reviews/approve", new { level, reviewId = review.ReviewId }, ct);
            }
        }
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(MessageState.Completed, (await verify.Messages.SingleAsync(m => m.Id == setup.Message, ct)).State);
        Assert.False(await verify.Assignments.AnyAsync(a => a.EndedAt == null, ct));
        Assert.All(await verify.Reviews.ToListAsync(ct), r => Assert.Equal(ReviewStatus.Approved, r.Status));
        Assert.Equal(skipSecond ? 2 : 3, await verify.Assignments.CountAsync(ct));
    }

    [Fact]
    public async Task CandidatesRespectReservationsAndUndoPreservesFutureAssignments()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await ConfigureLevels(fixture, setup.Message, ConfirmationMode.Immediate, AssignmentRetentionMode.PerConfirmation, false, ct, false);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        SetActor(client, "theo.mercer");
        await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Assignee, level = 2 }, ct);
        var candidates = (await client.GetFromJsonAsync<AssignmentCandidateDto[]>($"/api/messages/{setup.Message}/assignment-candidates?level=1", LevelJsonOptions, ct))!;
        Assert.DoesNotContain(candidates, c => c.Id == setup.Assignee);
        var duplicate = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Assignee, level = 1 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Actor, level = 1 }, ct);
        await PostOk(client, $"/api/messages/{setup.Message}/reviews/confirm", new { level = 1 }, ct);
        await using var db = new ORPDbContext(fixture.Options);
        var reviewId = await db.Reviews.Select(r => r.Id).SingleAsync(ct);
        await PostOk(client, $"/api/messages/{setup.Message}/reviews/undo", new { reviewId }, ct);
        var details = (await client.GetFromJsonAsync<MessageDetailsDto>($"/api/messages/{setup.Message}", LevelJsonOptions, ct))!;
        Assert.Null(details.CurrentAssigneeId);
        Assert.Equal(setup.Assignee, Assert.Single(details.Assignments).AssignedTo);
        Assert.Equal(2, details.Assignments[0].Level);
        await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Actor, level = 1 }, ct);
        await PostOk(client, $"/api/messages/{setup.Message}/reviews/confirm", new { level = 1 }, ct);
        var next = (await client.GetFromJsonAsync<MessageDetailsDto>($"/api/messages/{setup.Message}", LevelJsonOptions, ct))!;
        Assert.Equal(setup.Assignee, next.CurrentAssigneeId);
    }

    [Fact]
    public async Task ActiveSessionAllowsFutureReassignmentAndRejectClosesAllLevels()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await ConfigureLevels(fixture, setup.Message, ConfirmationMode.ReviewSession, AssignmentRetentionMode.PerConfirmation, false, ct);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        SetActor(client, "theo.mercer");
        var start = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/start", new { level = 1 }, ct);
        start.EnsureSuccessStatusCode();
        var review = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(ct))!;
        await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Assignee, level = 2 }, ct);
        await PostOk(client, $"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Actor, level = 2 }, ct);
        var current = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/assign", new { assignedTo = setup.Assignee, level = 1 }, ct);
        Assert.Equal(HttpStatusCode.Conflict, current.StatusCode);
        await PostOk(client, $"/api/messages/{setup.Message}/reviews/cancel", new { level = 1, reviewId = review.ReviewId }, ct);
        var cancelled = (await client.GetFromJsonAsync<MessageDetailsDto>($"/api/messages/{setup.Message}", LevelJsonOptions, ct))!;
        Assert.Equal(2, cancelled.Assignments.Count);
        start = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/start", new { level = 1 }, ct);
        start.EnsureSuccessStatusCode();
        review = (await start.Content.ReadFromJsonAsync<StartReviewResponse>(ct))!;
        await PostOk(client, $"/api/messages/{setup.Message}/reviews/reject", new { level = 1, reviewId = review.ReviewId }, ct);
        await using var db = new ORPDbContext(fixture.Options);
        Assert.False(await db.Assignments.AnyAsync(a => a.EndedAt == null, ct));
        Assert.Equal(MessageState.Rejected, (await db.Messages.SingleAsync(m => m.Id == setup.Message, ct)).State);
        var reassigned = await db.AuditEvents.SingleAsync(a => a.EventType == AuditEventType.MessageReassigned, ct);
        Assert.Contains("\"reviewLevel\":2", reassigned.DetailsJson);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task LevelMigrationPreservesCurrentAssignmentAndUnknownHistory(int level)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await ConfigureLevels(fixture, setup.Message, ConfirmationMode.Immediate,
            AssignmentRetentionMode.UntilWorkflowCompleted, false, ct);
        await using (var advance = new ORPDbContext(fixture.Options))
            for (var previous = 1; previous < level; previous++)
                await Handlers(advance, setup.Actor).Confirm.HandleAsync(setup.Message, new(previous), ct);
        await using var db = new ORPDbContext(fixture.Options);
        var old = new Assignment(setup.Message, setup.Actor, setup.Assignee, DateTimeOffset.UtcNow);
        old.End(DateTimeOffset.UtcNow);
        db.Assignments.Add(old);
        await db.SaveChangesAsync(ct);
        var migrator = db.GetService<IMigrator>();
        var migrations = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        Assert.EndsWith("_AssignmentsByLevel", migrations[^1]);
        await migrator.MigrateAsync(migrations[^2], ct);
        await migrator.MigrateAsync(cancellationToken: ct);
        db.ChangeTracker.Clear();
        var rows = await db.Assignments.ToListAsync(ct);
        Assert.Equal(level, Assert.Single(rows, a => a.EndedAt is null).Level);
        Assert.All(rows.Where(a => a.EndedAt is not null), a => Assert.Null(a.Level));
        Assert.Equal(setup.Actor, (await db.Messages.SingleAsync(m => m.Id == setup.Message, ct)).CurrentAssigneeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentFutureAssignmentAndMutationPreserveBothResults(bool approve)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await ConfigureLevels(fixture, setup.Message, ConfirmationMode.Immediate, AssignmentRetentionMode.PerConfirmation, false, ct);
        var gate = new SaveGate();
        await using var first = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        await using var second = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        var a = AssignHandler(first, setup.Actor).HandleAsync(setup.Message, new(setup.Assignee, 3), ct);
        var b = approve ? Handlers(second, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), ct)
            : AssignHandler(second, setup.Actor).HandleAsync(setup.Message, new(setup.Actor, 2), ct);
        await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(45), ct);
        Assert.Equal(2, gate.Arrived);
        await using var db = new ORPDbContext(fixture.Options);
        var active = await db.Assignments.Where(a => a.EndedAt == null).ToListAsync(ct);
        Assert.Equal(setup.Assignee, Assert.Single(active, a => a.Level == 3).AssignedTo);
        var message = await db.Messages.SingleAsync(m => m.Id == setup.Message, ct);
        Assert.Equal(approve ? MessageState.WaitingForSecondReview : MessageState.Assigned, message.State);
        Assert.Equal(approve ? null : (int?)setup.Actor, message.CurrentAssigneeId);
        Assert.Equal(approve ? 1 : 3, active.Count);
    }

    [Fact]
    public async Task ConcurrentReassignmentOfFutureLevelKeepsOneOwnerAndCompleteHistory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await ConfigureLevels(fixture, setup.Message, ConfirmationMode.Immediate, AssignmentRetentionMode.PerConfirmation, false, ct);
        var gate = new SaveGate();
        await using var first = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        await using var second = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        await Task.WhenAll(
            AssignHandler(first, setup.Actor).HandleAsync(setup.Message, new(setup.Actor, 3), ct),
            AssignHandler(second, setup.Actor).HandleAsync(setup.Message, new(setup.Assignee, 3), ct))
            .WaitAsync(TimeSpan.FromSeconds(45), ct);
        Assert.Equal(2, gate.Arrived);
        await using var verify = new ORPDbContext(fixture.Options);
        var history = await verify.Assignments.Where(a => a.Level == 3).ToListAsync(ct);
        Assert.Equal(2, history.Count);
        Assert.Single(history, a => a.EndedAt is null);
        Assert.Single(history, a => a.EndedAt is not null);
        Assert.Equal(setup.Actor, (await verify.Messages.SingleAsync(m => m.Id == setup.Message, ct)).CurrentAssigneeId);
        Assert.Single(await verify.AuditEvents.Where(a => a.EventType == AuditEventType.MessageReassigned).ToListAsync(ct));
    }

    [Fact]
    public async Task FailedFutureAssignmentRollsBackVersionAndAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        byte[] version;
        await using (var db = new ORPDbContext(fixture.Options))
            version = (await db.Messages.SingleAsync(m => m.Id == setup.Message, ct)).RowVersion;
        var options = new DbContextOptionsBuilder<ORPDbContext>(fixture.Options).AddInterceptors(new FailAfterSave()).Options;
        await using (var db = new ORPDbContext(options))
            await Assert.ThrowsAsync<IOException>(() => AssignHandler(db, setup.Actor)
                .HandleAsync(setup.Message, new(setup.Assignee, 2), ct));
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(version, (await verify.Messages.SingleAsync(m => m.Id == setup.Message, ct)).RowVersion);
        Assert.False(await verify.Assignments.AnyAsync(a => a.Level == 2, ct));
        Assert.Equal(1, await verify.AuditEvents.CountAsync(a => a.EventType == AuditEventType.MessageAssigned, ct));
    }

    private static AssignMessageHandler AssignHandler(ORPDbContext db, int actor)
    {
        var log = new BusinessActionLog(NullLogger<ORPStore>.Instance);
        var store = new ORPStore(db, log);
        var user = new Actor(actor);
        var correlation = new Correlation();
        var users = new UserAuthorizationQueries(db);
        return new(store, users, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, new Clock()), new TransactionExecutor(db, log),
            new MessageAuthorizationService(store, users, user, correlation, NullLogger<MessageAuthorizationService>.Instance));
    }

    private static async Task ConfigureLevels(SqlImportDatabase fixture, long id, ConfirmationMode mode,
        AssignmentRetentionMode retention, bool skipSecond, CancellationToken ct, bool reuse = true)
    {
        await using var db = new ORPDbContext(fixture.Options);
        var workflow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Preassigned levels").AddStep(1, 1);
        if (!skipSecond) workflow.AddStep(2, 2);
        workflow.AddStep(3, 3);
        workflow.ConfigureConfirmation(mode, reuse, retention, true);
        workflow.ConfigureUndo(UndoApprovalMode.Latest, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync(ct);
        var message = await db.Messages.SingleAsync(m => m.Id == id, ct);
        message.ChangeWorkflow(workflow, []);
        await db.SaveChangesAsync(ct);
    }

    private static void SetActor(HttpClient client, string name)
    {
        client.DefaultRequestHeaders.Remove("X-Debug-User");
        client.DefaultRequestHeaders.Add("X-Debug-User", name);
    }

    private static async Task PostOk(HttpClient client, string url, object body, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(url, body, ct);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent,
            $"{url}: {response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
    }
}
