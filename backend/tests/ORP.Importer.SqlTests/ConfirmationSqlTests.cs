using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Application.Abstractions;
using ORP.Application.Grids;
using ORP.Application.Messages.Get;
using ORP.Application.Assignments;
using ORP.Application.Assignments.Assign;
using ORP.Application.Assignments.GetCandidates;
using ORP.Application.Authorization;
using ORP.Application.Reviews;
using ORP.Application.Messages.ChangeWorkflow;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
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
    [Fact]
    public async Task ConcurrentConfirmHasOnlyOneOwnerAndApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await PrepareCombinedAsync(fixture, setup.Message, "DOUBLE_CONFIRMATION", false, ct);
        var gate = new SaveGate();
        await using var firstDb = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        await using var secondDb = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        var first = Record.ExceptionAsync(() => Handlers(firstDb, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), ct));
        var second = Record.ExceptionAsync(() => Handlers(secondDb, setup.Assignee).Confirm.HandleAsync(setup.Message, new(1), ct));
        var errors = await Task.WhenAll(first.AsTask(), second.AsTask()).WaitAsync(TimeSpan.FromSeconds(45), ct);
        Assert.Equal(2, gate.Arrived);
        Assert.Single(errors, x => x is null);
        Assert.IsType<UnauthorizedAccessException>(Assert.Single(errors, x => x is not null));
        await using var verify = new ORPDbContext(fixture.Options);
        var message = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(MessageState.WaitingForSecondReview, message.State);
        var assignment = Assert.Single(await verify.Assignments.Where(a => a.EndedAt == null).ToListAsync(ct));
        var review = Assert.Single(await verify.Reviews.ToListAsync(ct));
        Assert.Equal(message.CurrentAssigneeId, assignment.AssignedTo);
        Assert.Equal(assignment.AssignedTo, review.ReviewerId);
        Assert.Equal(ReviewStatus.Approved, review.Status);
        Assert.Equal(2, await verify.AuditEvents.CountAsync(x => x.EventType == AuditEventType.MessageAssigned, ct));
        Assert.Equal(1, await verify.AuditEvents.CountAsync(x => x.EventType == AuditEventType.ReviewApproved, ct));
    }

    [Fact]
    public async Task ConfirmSaveFailureRollsBackBothActions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await PrepareCombinedAsync(fixture, setup.Message, "SINGLE_CONFIRMATION", false, ct);
        var options = new DbContextOptionsBuilder<ORPDbContext>(fixture.Options)
            .AddInterceptors(new FailOnSecondSave()).Options;
        await using (var db = new ORPDbContext(options))
            await Assert.ThrowsAsync<IOException>(() => Handlers(db, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), ct));
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Null((await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct)).CurrentAssigneeId);
        Assert.Empty(await verify.Assignments.ToListAsync(ct));
        Assert.Empty(await verify.Reviews.ToListAsync(ct));
        Assert.False(await verify.AuditEvents.AnyAsync(x => x.EventType == AuditEventType.MessageAssigned ||
            x.EventType == AuditEventType.ReviewApproved || x.EventType == AuditEventType.MessageCompleted, ct));
    }

    private sealed class FailOnSecondSave : SaveChangesInterceptor
    {
        private int saves;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => ++saves == 2
                ? throw new IOException("Injected confirmation save failure.") : ValueTask.FromResult(result);
    }

    [Theory]
    [InlineData("SINGLE_CONFIRMATION", 1, false)]
    [InlineData("SINGLE_CONFIRMATION", 1, true)]
    [InlineData("DOUBLE_CONFIRMATION", 2, false)]
    [InlineData("DOUBLE_CONFIRMATION", 2, true)]
    public async Task ConfirmHttpCompletesWithAssignmentPerLevel(string code, int levels, bool assigned)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, assigned);
        await PrepareCombinedAsync(fixture, setup.Message, code, assigned, ct);
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(x => x.Code == code, ct);
            workflow.ConfigureConfirmation(ConfirmationMode.Immediate, true, AssignmentRetentionMode.PerConfirmation, false);
            await db.SaveChangesAsync(ct);
        }
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        async Task CheckCapabilities(int? expectedLevel)
        {
            var details = (await client.GetFromJsonAsync<MessageDetailsDto>(
                $"/api/messages/{setup.Message}", LevelJsonOptions, ct))!;
            var view = (await client.GetFromJsonAsync<MessageViewDto>(
                $"/api/messages/{setup.Message}/view", LevelJsonOptions, ct))!;
            using var response = await client.PostAsJsonAsync("/api/messages/grid", new MessageGridRequest(Search: details.ExternalId), ct);
            response.EnsureSuccessStatusCode();
            var page = (await response.Content.ReadFromJsonAsync<PagedResult<MessageGridRowDto>>(LevelJsonOptions, ct))!;
            var row = Assert.Single(page.Items, x => x.Id == setup.Message);
            foreach (var actual in new[]
            {
                (details.ConfirmationMode, details.CanConfirm, details.ConfirmationLevel, details.CanReview),
                (view.ConfirmationMode, view.CanConfirm, view.ConfirmationLevel, view.CanReview),
                (row.ConfirmationMode, row.CanConfirm, row.ConfirmationLevel, row.CanReview)
            })
                Assert.Equal((ConfirmationMode.Immediate, expectedLevel.HasValue, expectedLevel, false), actual);
            if (!assigned || expectedLevel != 1)
            {
                Assert.Null(details.CurrentAssigneeId);
                Assert.Null(view.CurrentAssigneeId);
                Assert.Null(row.CurrentAssigneeId);
            }
        }
        for (var level = 1; level <= levels; level++)
        {
            await CheckCapabilities(level);
            using var response = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/confirm",
                new { level, comment = " Checked " }, ct);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        await CheckCapabilities(null);
        await using var verify = new ORPDbContext(fixture.Options);
        var message = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(MessageState.Completed, message.State);
        Assert.Null(message.CurrentAssigneeId);
        var assignments = await verify.Assignments.Where(x => x.MessageId == setup.Message).ToListAsync(ct);
        Assert.Equal(levels, assignments.Count);
        Assert.All(assignments, a => { Assert.Equal(setup.Actor, a.AssignedTo); Assert.NotNull(a.EndedAt); });
        var reviews = await verify.Reviews.Where(x => x.MessageId == setup.Message).ToListAsync(ct);
        Assert.Equal(levels, reviews.Count);
        Assert.All(reviews, review =>
        {
            Assert.Equal(setup.Actor, review.ReviewerId);
            Assert.Equal("Checked", review.Comment);
            Assert.Equal(ReviewStatus.Approved, review.Status);
        });
        var audit = await verify.AuditEvents.Where(x => x.MessageId == setup.Message).ToListAsync(ct);
        Assert.Equal(levels, audit.Count(x => x.EventType == AuditEventType.MessageAssigned));
        Assert.Equal(levels, audit.Count(x => x.EventType == AuditEventType.ReviewApproved));
        Assert.DoesNotContain(audit, x => x.EventType == AuditEventType.MessageReassigned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmNeverTakesAnotherReviewersMessage(bool administrator)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using var db = new ORPDbContext(fixture.Options);
        var username = administrator
            ? await db.Users.Where(x => x.IsGlobalAdministrator).Select(x => x.UserName).SingleAsync(ct)
            : "priya.nair";
        var originalVersion = (await db.Messages.SingleAsync(x => x.Id == setup.Message, ct)).RowVersion.ToArray();
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", username);
        using var response = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/confirm", new { level = 1 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        db.ChangeTracker.Clear();
        var message = await db.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(setup.Actor, message.CurrentAssigneeId);
        Assert.Equal(originalVersion, message.RowVersion);
        Assert.Empty(await db.Reviews.ToListAsync(ct));
        Assert.Single(await db.Assignments.ToListAsync(ct));
    }

    [Theory]
    [InlineData("message.assign", false)]
    [InlineData("message.assign", true)]
    [InlineData("review.level1", false)]
    [InlineData("review.level1", true)]
    [InlineData("message.view", false)]
    [InlineData("message.view", true)]
    public async Task ConfirmRequiresPermissionsForRequestedActions(string missingPermission, bool assigned)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, assigned);
        await using var db = new ORPDbContext(fixture.Options);
        await db.RolePermissions.Where(x => x.Permission.Name == missingPermission).ExecuteDeleteAsync(ct);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        using var response = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/confirm", new { level = 1 }, ct);
        if (assigned && missingPermission == "message.assign")
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Single(await db.Reviews.ToListAsync(ct));
            return;
        }
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await db.Reviews.ToListAsync(ct));
        Assert.Equal(assigned ? setup.Actor : (int?)null, (await db.Messages.SingleAsync(x => x.Id == setup.Message, ct)).CurrentAssigneeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCombinedConfirmationRollsBackAssignmentAndAudit(bool sessionMode)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await PrepareCombinedAsync(fixture, setup.Message, "DOUBLE_CONFIRMATION", false, ct);
        await using var db = new ORPDbContext(fixture.Options);
        if (sessionMode)
        {
            var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(x => x.Code == "DOUBLE_CONFIRMATION", ct);
            workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
            await db.SaveChangesAsync(ct);
        }
        var originalVersion = (await db.Messages.SingleAsync(x => x.Id == setup.Message, ct)).RowVersion.ToArray();
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        using var response = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/confirm",
            new { level = sessionMode ? 1 : 2 }, ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        db.ChangeTracker.Clear();
        var message = await db.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Null(message.CurrentAssigneeId);
        Assert.Equal(originalVersion, message.RowVersion);
        Assert.Empty(await db.Assignments.ToListAsync(ct));
        Assert.Empty(await db.Reviews.ToListAsync(ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.MessageId == setup.Message &&
            (x.EventType == AuditEventType.MessageAssigned || x.EventType == AuditEventType.ReviewStarted), ct));
    }

    private static async Task PrepareCombinedAsync(SqlImportDatabase fixture, long id, string code, bool assigned, CancellationToken ct)
    {
        await using var db = new ORPDbContext(fixture.Options);
        var message = await db.Messages.SingleAsync(x => x.Id == id, ct);
        var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(x => x.Code == code, ct);
        message.ChangeWorkflow(workflow, []);
        if (!assigned && message.CurrentAssigneeId is not null)
        {
            message.Unassign();
            await db.Assignments.Where(x => x.MessageId == id).ExecuteDeleteAsync(ct);
            await db.AuditEvents.Where(x => x.MessageId == id && x.EventType == AuditEventType.MessageAssigned).ExecuteDeleteAsync(ct);
        }
        await db.SaveChangesAsync(ct);
    }

    [Fact]
    public async Task DatabaseRowVersionStillRejectsStaleWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using var first = new ORPDbContext(fixture.Options);
        await using var stale = new ORPDbContext(fixture.Options);
        var currentMessage = await first.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        var staleMessage = await stale.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        var originalVersion = staleMessage.RowVersion.ToArray();
        currentMessage.Assign(setup.Assignee);
        await first.SaveChangesAsync(ct);
        Assert.False(originalVersion.SequenceEqual(currentMessage.RowVersion));
        // Force an update from the context that read the previous rowversion.
        staleMessage.Unassign();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(ct));
        await using var verify = new ORPDbContext(fixture.Options);
        var persisted = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(setup.Assignee, persisted.CurrentAssigneeId);
        Assert.Equal(currentMessage.RowVersion, persisted.RowVersion);
    }

    [Theory]
    [InlineData("SINGLE_CONFIRMATION", 1)]
    [InlineData("DOUBLE_CONFIRMATION", 2)]
    public async Task OnlyCurrentAssigneeCanConfirmThroughHttp(string code, int levels)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var message = await db.Messages.SingleAsync(x => x.Id == setup.Message, ct);
            var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(x => x.Code == code, ct);
            message.ChangeWorkflow(workflow, []);
            await db.SaveChangesAsync(ct);
        }
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        var path = $"/api/messages/{setup.Message}";
        var details = await client.GetFromJsonAsync<JsonElement>(path, ct);
        using var assigned = await client.PostAsJsonAsync(path + "/assign",
            new { assignedTo = setup.Assignee }, ct);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        details = await client.GetFromJsonAsync<JsonElement>(path, ct);
        Assert.False(details.GetProperty("canConfirm").GetBoolean());
        using var denied = await client.PostAsJsonAsync(path + "/reviews/confirm",
            new { level = 1 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        // The same message.assign permission permits taking the message back for self-review.
        using var selfAssigned = await client.PostAsJsonAsync(path + "/assign",
            new { assignedTo = setup.Actor }, ct);
        Assert.Equal(HttpStatusCode.NoContent, selfAssigned.StatusCode);
        for (var level = 1; level <= levels; level++)
        {
            details = await client.GetFromJsonAsync<JsonElement>(path, ct);
            Assert.True(details.GetProperty("canConfirm").GetBoolean());
            Assert.Equal(level, details.GetProperty("confirmationLevel").GetInt32());
            using var confirmed = await client.PostAsJsonAsync(path + "/reviews/confirm",
                new { level }, ct);
            Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        }
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(MessageState.Completed, (await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct)).State);
        var reviews = await verify.Reviews.Where(x => x.MessageId == setup.Message).ToListAsync(ct);
        Assert.Equal(levels, reviews.Count);
        Assert.All(reviews, review => Assert.Equal(setup.Actor, review.ReviewerId));
    }

    [Fact]
    public async Task AssignHttpNeedsOnlyAssigneeAndStillAdvancesRowVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var message = await db.Messages.SingleAsync(x => x.Id == setup.Message, ct);
            var store = new ORPStore(db, new BusinessActionLog(NullLogger<ORPStore>.Instance));
            await new AssignmentCoordinator(store, new Clock()).UnassignAsync(message, setup.Actor, "setup", ct);
            await db.SaveChangesAsync(ct);
        }
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        var path = $"/api/messages/{setup.Message}";
        using var invalid = await client.PostAsJsonAsync(path + "/assign", new { assignedTo = 0 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var initial = (await client.GetFromJsonAsync<JsonElement>(path, ct)).GetProperty("version").GetString()!;
        using var assigned = await client.PostAsJsonAsync(path + "/assign", new { assignedTo = setup.Assignee }, ct);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        var current = (await client.GetFromJsonAsync<JsonElement>(path, ct)).GetProperty("version").GetString()!;
        Assert.NotEqual(initial, current);
        using var reassigned = await client.PostAsJsonAsync(path + "/assign", new { assignedTo = setup.Actor }, ct);
        Assert.Equal(HttpStatusCode.NoContent, reassigned.StatusCode);
        var updated = (await client.GetFromJsonAsync<JsonElement>(path, ct)).GetProperty("version").GetString()!;
        Assert.NotEqual(current, updated);
        using var removed = await client.PostAsJsonAsync(path + "/reassign", new { assignedTo = setup.Assignee }, ct);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        await using var verify = new ORPDbContext(fixture.Options);
        var persisted = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(setup.Actor, persisted.CurrentAssigneeId);
        var assignments = await verify.Assignments.Where(x => x.MessageId == setup.Message).ToListAsync(ct);
        Assert.Equal(3, assignments.Count);
        Assert.Equal(setup.Actor, Assert.Single(assignments, x => x.EndedAt == null).AssignedTo);
        var audit = await verify.AuditEvents.Where(x => x.MessageId == setup.Message).ToListAsync(ct);
        Assert.Equal(2, audit.Count(x => x.EventType == AuditEventType.MessageAssigned));
        Assert.Single(audit, x => x.EventType == AuditEventType.MessageReassigned);
    }

    private sealed class AssignmentFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }

    [Fact]
    public async Task AssignmentPolicyMigrationPreservesExistingAssignments()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using var db = new ORPDbContext(fixture.Options);
        var migrator = db.GetService<IMigrator>();
        var migrations = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        var policyIndex = Array.FindIndex(migrations, m => m.EndsWith("_RequireAssignedReviewer"));
        await migrator.MigrateAsync(migrations[policyIndex - 1], ct);
        await db.Database.ExecuteSqlRawAsync("""
            DELETE rp FROM orp.RolePermissions rp
            JOIN orp.Roles r ON r.Id = rp.RoleId
            JOIN orp.Permissions p ON p.Id = rp.PermissionId
            WHERE r.Name = N'Reviewer' AND p.Name = N'message.assign';
            """, ct);
        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.True(await db.Roles.Where(x => x.Name == "Reviewer")
            .AnyAsync(x => x.Permissions.Any(p => p.Permission.Name == "message.assign"), ct));
        Assert.Equal(setup.Actor, (await db.Messages.SingleAsync(x => x.Id == setup.Message, ct)).CurrentAssigneeId);
        Assert.Equal(3, await db.WorkflowDefinitions.CountAsync(ct));
    }

    [Theory]
    [InlineData("SINGLE_CONFIRMATION", 1)]
    [InlineData("DOUBLE_CONFIRMATION", 2)]
    public async Task SelfAssignmentCandidateCanAssignAndCompleteOwnMessage(string code, int levels)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using var db = new ORPDbContext(fixture.Options);
        var message = await db.Messages.Include(x => x.Departments).SingleAsync(x => x.Id == setup.Message, ct);
        var log = new BusinessActionLog(NullLogger<ORPStore>.Instance);
        var store = new ORPStore(db, log);
        var assignments = new AssignmentCoordinator(store, new Clock());
        await assignments.UnassignAsync(message, setup.Actor, "setup", ct);
        var workflow = await db.WorkflowDefinitions.Include(x => x.Steps).SingleAsync(x => x.Code == code, ct);
        message.ChangeWorkflow(workflow, []);
        await db.SaveChangesAsync(ct);
        var user = new Actor(setup.Actor);
        var users = new UserAuthorizationQueries(db);
        var correlation = new Correlation();
        var authorization = new MessageAuthorizationService(store, users, user, correlation,
            NullLogger<MessageAuthorizationService>.Instance);
        var candidates = new GetAssignmentCandidatesHandler(new AssignmentCandidateQueries(db), store, authorization);
        Assert.Contains(await candidates.HandleAsync(message.Id, ct), x => x.Id == setup.Actor);
        var assign = new AssignMessageHandler(store, users, new AssignMessageValidator(), user, correlation,
            assignments, new TransactionExecutor(db, log), authorization);
        await assign.HandleAsync(message.Id, new(setup.Actor), ct);
        var activeAssignment = await db.Assignments.AsNoTracking().SingleAsync(x => x.MessageId == message.Id && x.EndedAt == null, ct);
        Assert.Equal(setup.Actor, activeAssignment.AssignedBy);
        Assert.Equal(setup.Actor, activeAssignment.AssignedTo);
        Assert.DoesNotContain(await candidates.HandleAsync(message.Id, ct), x => x.Id == setup.Actor);
        var access = (await new UserAccessService(db).GetByIdAsync(setup.Actor, ct))!;
        for (var level = 1; level <= levels; level++)
        {
            var details = await DetailsAsync(db, message.Id, access, ct);
            Assert.True(details.CanConfirm);
            Assert.Equal(level, details.ConfirmationLevel);
            await Handlers(db, setup.Actor).Confirm.HandleAsync(message.Id, new(level), ct);
        }
        db.ChangeTracker.Clear();
        var completed = await db.Messages.SingleAsync(x => x.Id == message.Id, ct);
        Assert.Equal(MessageState.Completed, completed.State);
        Assert.Null(completed.CurrentAssigneeId);
        var reviews = await db.Reviews.Where(x => x.MessageId == message.Id).ToListAsync(ct);
        Assert.Equal(levels, reviews.Count);
        Assert.All(reviews, review =>
        {
            Assert.Equal(setup.Actor, review.ReviewerId);
            Assert.Equal(ReviewStatus.Approved, review.Status);
        });
    }

    [Fact]
    public async Task ConfirmUndoAndRepeatPersistVersionsAssignmentAuditAndCapabilities()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        await using var db = new ORPDbContext(fixture.Options);
        var access = (await new UserAccessService(db).GetByIdAsync(setup.Actor, ct))!;
        var initial = await DetailsAsync(db, setup.Message, access, ct);
        Assert.True(initial.CanConfirm);
        Assert.False(initial.CanReview);
        Assert.Equal(1, initial.ConfirmationLevel);
        Assert.Equal(ConfirmationMode.Immediate, initial.ConfirmationMode);
        var grid = Assert.Single((await new MessageGridQueries(db).LoadAsync(new() { Search = "TEST-00001" }, access, ct)).Items);
        Assert.Equal(initial.Version, grid.Version);
        Assert.Equal(initial.CanConfirm, grid.CanConfirm);
        var handlers = Handlers(db, setup.Actor);
        await handlers.Confirm.HandleAsync(setup.Message, new(1, "first"), ct);
        var waiting = await DetailsAsync(db, setup.Message, access, ct);
        Assert.Equal(MessageState.WaitingForSecondReview, waiting.State);
        Assert.Equal(setup.Actor, waiting.CurrentAssigneeId);
        Assert.NotEqual(initial.Version, waiting.Version);
        Assert.Equal(2, waiting.ConfirmationLevel);
        Assert.True(waiting.CanConfirm); // the same user may confirm again
        var approvedId = await db.Reviews.Select(x => x.Id).SingleAsync(ct);
        await handlers.Undo.HandleAsync(setup.Message, new(approvedId), ct);
        var undone = await DetailsAsync(db, setup.Message, access, ct);
        Assert.Equal(MessageState.Assigned, undone.State);
        Assert.Equal(setup.Actor, undone.CurrentAssigneeId);
        Assert.NotEqual(initial.Version, undone.Version);
        await handlers.Confirm.HandleAsync(setup.Message, new(1), ct);
        var next = await DetailsAsync(db, setup.Message, access, ct);
        await handlers.Confirm.HandleAsync(setup.Message, new(2), ct);
        var completed = await DetailsAsync(db, setup.Message, access, ct);
        Assert.Equal(MessageState.Completed, completed.State);
        Assert.Null(completed.CurrentAssigneeId);
        Assert.False(completed.CanConfirm);
        Assert.Null(completed.ConfirmationLevel);
        var reviews = await db.Reviews.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        Assert.Equal(new[] { ReviewStatus.Undone, ReviewStatus.Approved, ReviewStatus.Approved }, reviews.Select(x => x.Status));
        Assert.All(reviews, x => Assert.Equal(setup.Actor, x.ReviewerId));
        Assert.All(await db.Assignments.AsNoTracking().ToListAsync(ct), x => Assert.NotNull(x.EndedAt));
        Assert.Equal(3, await db.AuditEvents.CountAsync(x => x.MessageId == setup.Message && x.EventType == AuditEventType.ReviewStarted, ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.MessageId == setup.Message && x.EventType == AuditEventType.MessageCompleted, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentConfirmAndCompetingOperationCommitExactlyOneResult(bool undo)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        long firstId = 0;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            if (undo)
            {
                await Handlers(db, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), ct);
                firstId = await db.Reviews.Select(x => x.Id).SingleAsync(ct);
            }
        }
        var gate = new SaveGate();
        await using var firstDb = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        await using var secondDb = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        var first = Handlers(firstDb, setup.Actor);
        var second = Handlers(secondDb, setup.Actor);
        var request = new ConfirmReviewRequest(undo ? 2 : 1);
        var firstTask = Record.ExceptionAsync(() => first.Confirm.HandleAsync(setup.Message, request, ct));
        var secondTask = Record.ExceptionAsync(() => undo
            ? second.Undo.HandleAsync(setup.Message, new(firstId), ct)
            : second.Confirm.HandleAsync(setup.Message, request, ct));
        var errors = await Task.WhenAll(firstTask.AsTask(), secondTask.AsTask()).WaitAsync(TimeSpan.FromSeconds(45), ct);
        Assert.Equal(2, gate.Arrived);
        Assert.Single(errors, x => x is null);
        Assert.IsType<DomainRuleViolationException>(Assert.Single(errors, x => x is not null));
        await using var verify = new ORPDbContext(fixture.Options);
        var message = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        var reviews = await verify.Reviews.OrderBy(x => x.Id).ToListAsync(ct);
        Assert.DoesNotContain(reviews, x => x.Status == ReviewStatus.InProgress);
        if (!undo)
        {
            Assert.Equal(MessageState.WaitingForSecondReview, message.State);
            Assert.Equal(ReviewStatus.Approved, Assert.Single(reviews).Status);
            Assert.Equal(setup.Actor, message.CurrentAssigneeId);
        }
        else if (message.State == MessageState.Completed)
        {
            Assert.Equal(2, reviews.Count);
            Assert.All(reviews, x => Assert.Equal(ReviewStatus.Approved, x.Status));
            Assert.Null(message.CurrentAssigneeId);
            Assert.False(await verify.AuditEvents.AnyAsync(x => x.EventType == AuditEventType.ConfirmationUndone, ct));
        }
        else
        {
            Assert.Equal(MessageState.Assigned, message.State);
            Assert.Equal(ReviewStatus.Undone, Assert.Single(reviews).Status);
            Assert.Equal(setup.Actor, message.CurrentAssigneeId);
            Assert.Equal(1, await verify.AuditEvents.CountAsync(x => x.EventType == AuditEventType.ConfirmationUndone, ct));
        }
        Assert.Equal(reviews.Count, await verify.AuditEvents.CountAsync(x => x.EventType == AuditEventType.ReviewApproved, ct));
    }

    [Fact]
    public async Task FailureAfterSaveRollsBackReviewStateAndAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        string version;
        await using (var read = new ORPDbContext(fixture.Options))
            version = Convert.ToBase64String((await read.Messages.SingleAsync(x => x.Id == setup.Message, ct)).RowVersion);
        var options = new DbContextOptionsBuilder<ORPDbContext>(fixture.Options).AddInterceptors(new FailAfterSave()).Options;
        await using (var db = new ORPDbContext(options))
            await Assert.ThrowsAsync<IOException>(() => Handlers(db, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), ct));
        await using var verify = new ORPDbContext(fixture.Options);
        var message = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Equal(setup.Actor, message.CurrentAssigneeId);
        Assert.Empty(await verify.Reviews.ToListAsync(ct));
        Assert.False(await verify.AuditEvents.AnyAsync(x => x.EventType == AuditEventType.ReviewStarted || x.EventType == AuditEventType.ReviewApproved, ct));
        Assert.Equal(version, Convert.ToBase64String(message.RowVersion));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessingScopeRefreshUpdatesVersionAtomically(bool failAfterSave)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        int workflowId, tfoId, adminId;
        string initialVersion;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var message = await db.Messages.SingleAsync(x => x.Id == setup.Message, ct);
            workflowId = message.WorkflowDefinitionId;
            tfoId = await db.Departments.Where(x => x.Name == "TFO").Select(x => x.Id).SingleAsync(ct);
            adminId = await db.Users.Where(x => x.IsGlobalAdministrator).Select(x => x.Id).SingleAsync(ct);
            var viewOnly = await db.WorkflowDefinitions.SingleAsync(x => x.Code == "VIEW_ONLY", ct);
            db.MessageDepartments.Add(new MessageDepartment(message.Id, tfoId, true));
            db.DepartmentRoutingRules.Add(new DepartmentRoutingRule
            {
                DepartmentId = tfoId, Direction = 1, MatchKind = 1, MessageTypePattern = "MT199",
                IsActive = true, WorkflowDefinitionId = viewOnly.Id,
                SourceFile = "scope-refresh-test", SourceSheet = "rules", SourceRow = 1
            });
            await db.SaveChangesAsync(ct);
            initialVersion = Convert.ToBase64String(message.RowVersion);
        }
        var options = failAfterSave
            ? new DbContextOptionsBuilder<ORPDbContext>(fixture.Options).AddInterceptors(new FailAfterSave()).Options
            : fixture.Options;
        await using (var db = new ORPDbContext(options))
        {
            var change = ChangeHandler(db, adminId);
            if (failAfterSave)
                await Assert.ThrowsAsync<IOException>(() => change.HandleAsync(setup.Message, new(workflowId), ct));
            else
                await change.HandleAsync(setup.Message, new(workflowId), ct);
        }
        await using var verify = new ORPDbContext(fixture.Options);
        var persisted = await verify.Messages.Include(x => x.Departments).SingleAsync(x => x.Id == setup.Message, ct);
        var version = Convert.ToBase64String(persisted.RowVersion);
        Assert.Equal(workflowId, persisted.WorkflowDefinitionId);
        Assert.Equal(MessageState.Assigned, persisted.State);
        Assert.Equal(failAfterSave ? setup.Actor : (int?)null, persisted.CurrentAssigneeId);
        Assert.Equal(failAfterSave, persisted.Departments.Single(x => x.DepartmentId == tfoId).CanProcess);
        Assert.Equal(failAfterSave ? 0 : 1, await verify.AuditEvents.CountAsync(x =>
            x.MessageId == setup.Message && x.EventType == AuditEventType.MessageWorkflowChanged, ct));
        Assert.Empty(await verify.Reviews.ToListAsync(ct));
        if (failAfterSave)
        {
            Assert.Equal(initialVersion, version);
            return;
        }
        Assert.NotEqual(initialVersion, version);
        // An unchanged scope must not invalidate versions or create another audit event.
        await ChangeHandler(verify, adminId).HandleAsync(setup.Message, new(workflowId), ct);
        var access = (await new UserAccessService(verify).GetByIdAsync(setup.Actor, ct))!;
        Assert.Equal(version, (await DetailsAsync(verify, setup.Message, access, ct)).Version);
        Assert.Equal(1, await verify.AuditEvents.CountAsync(x =>
            x.MessageId == setup.Message && x.EventType == AuditEventType.MessageWorkflowChanged, ct));
        await Handlers(verify, setup.Actor).Confirm.HandleAsync(setup.Message, new(1), ct);
        Assert.Equal(MessageState.WaitingForSecondReview,
            (await DetailsAsync(verify, setup.Message, access, ct)).State);
    }

    private static ChangeMessageWorkflowHandler ChangeHandler(ORPDbContext db, int actor)
    {
        var log = new BusinessActionLog(NullLogger<ORPStore>.Instance);
        var store = new ORPStore(db, log);
        var user = new Actor(actor);
        var correlation = new Correlation();
        var authorization = new MessageAuthorizationService(store, new UserAuthorizationQueries(db), user,
            correlation, NullLogger<MessageAuthorizationService>.Instance);
        return new(store, new ChangeMessageWorkflowValidator(), new WorkflowResolver(db), user,
            new Clock(), correlation, new TransactionExecutor(db, log), authorization, new AssignmentCoordinator(store, new Clock()));
    }

    private static async Task<MessageDetailsDto> DetailsAsync(ORPDbContext db, long id, UserAccess access, CancellationToken ct) =>
        (await new MessageQueries(db).GetAsync(id, access, ct))!;

    private static async Task<(long Message, int Actor, int Assignee)> SeedAsync(SqlImportDatabase fixture, CancellationToken ct, bool assigned = true)
    {
        await using var db = new ORPDbContext(fixture.Options);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-test-data.sql"), ct);
        await db.Database.OpenConnectionAsync(ct);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = script;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(ct);
        }
        var actor = await db.Users.SingleAsync(x => x.UserName == "theo.mercer", ct);
        var assignee = await db.Users.SingleAsync(x => x.UserName == "priya.nair", ct);
        var message = await db.Messages.SingleAsync(x => x.Id == db.SwiftMessages.Where(s => s.ReferenceId == "TEST-00001").Select(s => s.MessageId).Single(), ct);
        var log = new BusinessActionLog(NullLogger<ORPStore>.Instance);
        var store = new ORPStore(db, log);
        if (assigned)
            await new AssignmentCoordinator(store, new Clock()).AssignAsync(message, actor.Id, actor.Id, "setup", ct);
        await db.SaveChangesAsync(ct);
        return (message.Id, actor.Id, assignee.Id);
    }

    private static (ConfirmReviewHandler Confirm, UndoReviewHandler Undo) Handlers(ORPDbContext db, int actor)
    {
        var log = new BusinessActionLog(NullLogger<ORPStore>.Instance);
        var store = new ORPStore(db, log);
        var user = new Actor(actor);
        var clock = new Clock();
        var correlation = new Correlation();
        var assignments = new AssignmentCoordinator(store, clock);
        var transactions = new TransactionExecutor(db, log);
        var authorization = new MessageAuthorizationService(store, new UserAuthorizationQueries(db), user,
            correlation, NullLogger<MessageAuthorizationService>.Instance);
        var assign = new AssignMessageHandler(store, new UserAuthorizationQueries(db), new AssignMessageValidator(), user,
            correlation, assignments, transactions, authorization);
        var confirm = new ConfirmReviewHandler(store, new ConfirmReviewValidator(), user, clock, correlation, assignments, transactions, authorization, assign);
        return (confirm,
            new(store, new UndoReviewValidator(), user, clock, correlation, assignments, transactions, authorization));
    }

    private static DbContextOptions<ORPDbContext> RetryOptions(SqlImportDatabase fixture, IInterceptor interceptor) =>
        new DbContextOptionsBuilder<ORPDbContext>().UseSqlServer(fixture.ConnectionString,
            sql => sql.EnableRetryOnFailure(3, TimeSpan.FromMilliseconds(100), null)).AddInterceptors(interceptor).Options;

    private sealed record Actor(int UserId) : ICurrentUser
    {
        public string UserName => "test";
        public string DisplayName => "Test actor";
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Correlation : ICorrelationContext { public string CorrelationId => "sql-confirmation"; }
    private sealed class SaveGate
    {
        private int arrived;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Arrived => arrived;
        public async Task MeetAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrived) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
        }
    }
    private sealed class MeetBeforeSave(SaveGate gate) : SaveChangesInterceptor
    {
        private int seen;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref seen, 1) == 0) await gate.MeetAsync(cancellationToken);
            return result;
        }
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default) => throw new IOException("Injected failure before commit.");
    }
}
