using ORP.Application.Grids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
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

public sealed class ConfirmationSqlTests
{
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
        var grid = Assert.Single((await new MessageGridQueries(db).LoadAsync(new() { Take = 20, Filter = new object[] { "ExternalId", "contains", "TEST-00001" } }, access, null, ct)).data.Cast<MessageGridRowDto>());
        Assert.Equal(initial.Version, grid.Version);
        Assert.Equal(initial.CanConfirm, grid.CanConfirm);
        var handlers = Handlers(db, setup.Actor);
        await handlers.Confirm.HandleAsync(setup.Message, new(1, initial.Version, "first"), ct);
        var waiting = await DetailsAsync(db, setup.Message, access, ct);
        Assert.Equal(MessageState.WaitingForSecondReview, waiting.State);
        Assert.Equal(setup.Assignee, waiting.CurrentAssigneeId);
        Assert.NotEqual(initial.Version, waiting.Version);
        Assert.Equal(2, waiting.ConfirmationLevel);
        Assert.True(waiting.CanConfirm); // the same user may confirm again
        var approvedId = await db.Reviews.Select(x => x.Id).SingleAsync(ct);
        await handlers.Undo.HandleAsync(setup.Message, new(approvedId), ct);
        var undone = await DetailsAsync(db, setup.Message, access, ct);
        Assert.Equal(MessageState.Assigned, undone.State);
        Assert.Equal(setup.Assignee, undone.CurrentAssigneeId);
        Assert.NotEqual(initial.Version, undone.Version);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => handlers.Confirm.HandleAsync(setup.Message, new(1, initial.Version), ct));
        await handlers.Confirm.HandleAsync(setup.Message, new(1, undone.Version), ct);
        var next = await DetailsAsync(db, setup.Message, access, ct);
        await handlers.Confirm.HandleAsync(setup.Message, new(2, next.Version), ct);
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
        string version;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            var access = (await new UserAccessService(db).GetByIdAsync(setup.Actor, ct))!;
            var initial = await DetailsAsync(db, setup.Message, access, ct);
            if (undo)
            {
                await Handlers(db, setup.Actor).Confirm.HandleAsync(setup.Message, new(1, initial.Version), ct);
                firstId = await db.Reviews.Select(x => x.Id).SingleAsync(ct);
            }
            version = (await DetailsAsync(db, setup.Message, access, ct)).Version;
        }
        var gate = new SaveGate();
        await using var firstDb = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        await using var secondDb = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        var first = Handlers(firstDb, setup.Actor);
        var second = Handlers(secondDb, setup.Actor);
        var request = new ConfirmReviewRequest(undo ? 2 : 1, version);
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
            Assert.Equal(setup.Assignee, message.CurrentAssigneeId);
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
            Assert.Equal(setup.Assignee, message.CurrentAssigneeId);
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
            await Assert.ThrowsAsync<IOException>(() => Handlers(db, setup.Actor).Confirm.HandleAsync(setup.Message, new(1, version), ct));
        await using var verify = new ORPDbContext(fixture.Options);
        var message = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Equal(setup.Assignee, message.CurrentAssigneeId);
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
        Assert.Equal(setup.Assignee, persisted.CurrentAssigneeId);
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
        var error = await Assert.ThrowsAsync<DomainRuleViolationException>(() =>
            Handlers(verify, setup.Actor).Confirm.HandleAsync(setup.Message, new(1, initialVersion), ct));
        Assert.Contains("version has changed", error.Message);
        Assert.Empty(await verify.Reviews.ToListAsync(ct));
        await Handlers(verify, setup.Actor).Confirm.HandleAsync(setup.Message, new(1, version), ct);
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
            new Clock(), correlation, new TransactionExecutor(db, log), authorization);
    }

    private static async Task<MessageDetailsDto> DetailsAsync(ORPDbContext db, long id, UserAccess access, CancellationToken ct) =>
        (await new MessageQueries(db).GetAsync(id, access, ct))!;

    private static async Task<(long Message, int Actor, int Assignee)> SeedAsync(SqlImportDatabase fixture, CancellationToken ct)
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
        await new AssignmentCoordinator(store, new Clock()).AssignAsync(message, assignee.Id, actor.Id, "setup", ct);
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
        return (new(store, new ConfirmReviewValidator(), user, clock, correlation, assignments, transactions, authorization),
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
