using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Authorization;
using ORP.Application.Reviews;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
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
    [Theory]
    [InlineData(1, false, AssignmentRetentionMode.PerConfirmation)]
    [InlineData(2, false, AssignmentRetentionMode.PerConfirmation)]
    [InlineData(3, false, AssignmentRetentionMode.PerConfirmation)]
    [InlineData(3, true, AssignmentRetentionMode.PerConfirmation)]
    [InlineData(3, false, AssignmentRetentionMode.UntilWorkflowCompleted)]
    public async Task BulkHttpApprovesEveryRequiredLevelWithNormalAudit(int count, bool skipSecond, AssignmentRetentionMode retention)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        var levels = Enumerable.Range(1, count).Where(level => !skipSecond || level != 2).ToArray();
        await ConfigureBulkAsync(fixture, [setup.Message], levels, retention, ct);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "bulk-http");
        using var response = await client.PostAsJsonAsync("/api/messages/reviews/bulk-approve",
            new { messageIds = new[] { setup.Message }, comment = " Checked " }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BulkApproveReviewsResponse>(ct))!;
        var result = Assert.Single(body.Results);
        Assert.Equal(200, result.Status);
        Assert.Equal(levels, result.ApprovedLevels);
        Assert.Null(result.Error);
        await using var verify = new ORPDbContext(fixture.Options);
        var message = await verify.Messages.SingleAsync(x => x.Id == setup.Message, ct);
        Assert.Equal(MessageState.Completed, message.State);
        Assert.Null(message.CurrentAssigneeId);
        Assert.All(await verify.Reviews.ToListAsync(ct), r =>
        {
            Assert.Equal(setup.Actor, r.ReviewerId);
            Assert.Equal(ReviewStatus.Approved, r.Status);
            Assert.Equal("Checked", r.Comment);
        });
        var history = await verify.Assignments.Where(a => a.MessageId == setup.Message).OrderBy(a => a.Level).ToListAsync(ct);
        Assert.Equal(levels, history.Select(a => a.Level!.Value));
        Assert.All(history, a => { Assert.Equal(setup.Actor, a.AssignedTo); Assert.NotNull(a.EndedAt); });
        var events = await verify.AuditEvents.Where(a => a.MessageId == setup.Message && a.EventType != AuditEventType.MessageRegistered).ToListAsync(ct);
        Assert.Equal(levels.Length, events.Count(a => a.EventType == AuditEventType.ReviewStarted));
        Assert.Equal(levels.Length, events.Count(a => a.EventType == AuditEventType.ReviewApproved));
        Assert.Single(events, a => a.EventType == AuditEventType.MessageCompleted);
        Assert.All(events, a => { Assert.Equal(setup.Actor, a.UserId); Assert.Equal("bulk-http", a.CorrelationId); });
        using var retry = await client.PostAsJsonAsync("/api/messages/reviews/bulk-approve", new { messageIds = new[] { setup.Message } }, ct);
        Assert.Equal(409, Assert.Single((await retry.Content.ReadFromJsonAsync<BulkApproveReviewsResponse>(ct))!.Results).Status);
        Assert.Equal(levels.Length, await verify.Reviews.CountAsync(ct));
    }

    [Fact]
    public async Task BulkLateSaveFailureRollsBackOneMessageAndContinuesBatch()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        long other;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            other = await db.Messages.Where(m => m.Id != setup.Message && m.State == MessageState.New).Select(m => m.Id).FirstAsync(ct);
        }
        await ConfigureBulkAsync(fixture, [setup.Message, other], [1, 2, 3], AssignmentRetentionMode.PerConfirmation, ct);
        byte[] version;
        await using (var read = new ORPDbContext(fixture.Options))
            version = (await read.Messages.SingleAsync(m => m.Id == setup.Message, ct)).RowVersion.ToArray();
        var options = new DbContextOptionsBuilder<ORPDbContext>(fixture.Options)
            .AddInterceptors(new FailOnBulkSecondApproval(setup.Message)).Options;
        await using (var db = new ORPDbContext(options))
        {
            var results = (await BulkHandler(db, setup.Actor).HandleAsync(new([setup.Message, long.MaxValue, other]), ct)).Results;
            Assert.Equal(new[] { 500, 404, 200 }, results.Select(r => r.Status));
            Assert.Equal("unexpected_error", results[0].Error?.Code);
            Assert.DoesNotContain("Injected", results[0].Error!.Detail);
        }
        await using var verify = new ORPDbContext(fixture.Options);
        var failed = await verify.Messages.SingleAsync(m => m.Id == setup.Message, ct);
        Assert.Equal(MessageState.New, failed.State);
        Assert.Null(failed.CurrentAssigneeId);
        Assert.Equal(version, failed.RowVersion);
        Assert.False(await verify.Assignments.AnyAsync(a => a.MessageId == setup.Message, ct));
        Assert.False(await verify.Reviews.AnyAsync(r => r.MessageId == setup.Message, ct));
        Assert.False(await verify.AuditEvents.AnyAsync(a => a.MessageId == setup.Message && a.EventType != AuditEventType.MessageRegistered, ct));
        Assert.Equal(MessageState.Completed, (await verify.Messages.SingleAsync(m => m.Id == other, ct)).State);
        Assert.Equal(3, await verify.Reviews.CountAsync(r => r.MessageId == other, ct));
    }

    [Theory]
    [InlineData(Permissions.ReviewBulkApprove)]
    [InlineData(Permissions.MessageAssign)]
    [InlineData(Permissions.MessageView)]
    [InlineData(Permissions.ReviewLevel3)]
    public async Task BulkHttpRequiresAllScopedPermissionsBeforeAnyApproval(string missing)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await ConfigureBulkAsync(fixture, [setup.Message], [1, 2, 3], AssignmentRetentionMode.PerConfirmation, ct);
        await using var db = new ORPDbContext(fixture.Options);
        await db.RolePermissions.Where(rp => rp.Permission.Name == missing).ExecuteDeleteAsync(ct);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        using var response = await client.PostAsJsonAsync("/api/messages/reviews/bulk-approve", new { messageIds = new[] { setup.Message } }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(403, Assert.Single((await response.Content.ReadFromJsonAsync<BulkApproveReviewsResponse>(ct))!.Results).Status);
        Assert.Empty(await db.Assignments.ToListAsync(ct));
        Assert.Empty(await db.Reviews.ToListAsync(ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BulkRejectsPermissionFromAnotherBranchOrDepartment(bool wrongBranch)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await using var db = new ORPDbContext(fixture.Options);
        var message = await db.Messages.Include(m => m.Departments).SingleAsync(m => m.Id == setup.Message, ct);
        var branch = wrongBranch ? await db.Branches.Where(b => b.Id != message.BranchId).Select(b => b.Id).FirstAsync(ct) : message.BranchId!.Value;
        var department = wrongBranch ? message.ProcessingDepartmentIds[0] :
            await db.Departments.Where(d => !message.ProcessingDepartmentIds.Contains(d.Id)).Select(d => d.Id).FirstAsync(ct);
        var role = await db.Roles.Where(r => r.Name == "Reviewer").Select(r => r.Id).SingleAsync(ct);
        await db.UserRoles.Where(ur => ur.UserId == setup.Actor).ExecuteDeleteAsync(ct);
        db.UserRoles.Add(new UserRole { UserId = setup.Actor, BranchId = branch, DepartmentId = department, RoleId = role });
        await db.SaveChangesAsync(ct);
        Assert.Equal(403, Assert.Single((await BulkHandler(db, setup.Actor).HandleAsync(new([setup.Message]), ct)).Results).Status);
        Assert.Empty(await db.Reviews.ToListAsync(ct));
        Assert.Empty(await db.Assignments.ToListAsync(ct));
    }

    [Fact]
    public async Task BulkHttpInvalidBatchNeverChangesAnyMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await using var factory = new AssignmentFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        BulkApproveReviewsRequest[] invalid = [new(null!), new([]), new([setup.Message, 0]), new([setup.Message, setup.Message]),
            new(Enumerable.Range(1, 101).Select(i => (long)i).ToArray()), new([setup.Message], new string('x', 2001))];
        foreach (var request in invalid)
        {
            using var response = await client.PostAsJsonAsync("/api/messages/reviews/bulk-approve", request, ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
            Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
        }
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Empty(await verify.Assignments.ToListAsync(ct));
        Assert.Empty(await verify.Reviews.ToListAsync(ct));
        Assert.Equal(100, await verify.AuditEvents.CountAsync(ct));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task BulkRejectsOtherAssigneeIncludingFutureLevel(int level)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await ConfigureBulkAsync(fixture, [setup.Message], [1, 2, 3], AssignmentRetentionMode.PerConfirmation, ct);
        await using (var db = new ORPDbContext(fixture.Options))
            await AssignHandler(db, setup.Actor).HandleAsync(setup.Message, new(setup.Assignee, level), ct);
        await using (var db = new ORPDbContext(fixture.Options))
            Assert.Equal(409, Assert.Single((await BulkHandler(db, setup.Actor).HandleAsync(new([setup.Message]), ct)).Results).Status);
        await using var verify = new ORPDbContext(fixture.Options);
        var assignment = Assert.Single(await verify.Assignments.ToListAsync(ct));
        Assert.Equal(setup.Assignee, assignment.AssignedTo);
        Assert.Null(assignment.EndedAt);
        Assert.Empty(await verify.Reviews.ToListAsync(ct));
    }

    [Fact]
    public async Task BulkPreservesPreviousApprovalAndOwnFutureAssignment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, true);
        await ConfigureBulkAsync(fixture, [setup.Message], [1, 2, 3], AssignmentRetentionMode.PerConfirmation, ct);
        long originalReview, futureAssignment;
        await using (var db = new ORPDbContext(fixture.Options))
        {
            // Assign before approval, while the normal four-eyes rule still permits this user.
            var log = new BusinessActionLog(NullLogger<ORPStore>.Instance);
            var message = await db.Messages.SingleAsync(m => m.Id == setup.Message, ct);
            await new AssignmentCoordinator(new ORPStore(db, log), new Clock()).AssignAsync(message, setup.Actor, setup.Actor, "setup", ct, 3);
            await db.SaveChangesAsync(ct);
            futureAssignment = await db.Assignments.Where(a => a.Level == 3).Select(a => a.Id).SingleAsync(ct);
            await Handlers(db, setup.Actor).Confirm.HandleAsync(setup.Message, new(1, "original"), ct);
            originalReview = await db.Reviews.Select(r => r.Id).SingleAsync(ct);
            var result = Assert.Single((await BulkHandler(db, setup.Actor).HandleAsync(new([setup.Message], "bulk"), ct)).Results);
            Assert.Equal(new[] { 2, 3 }, result.ApprovedLevels);
        }
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal("original", (await verify.Reviews.SingleAsync(r => r.Id == originalReview, ct)).Comment);
        Assert.NotNull((await verify.Assignments.SingleAsync(a => a.Id == futureAssignment, ct)).EndedAt);
        Assert.Equal(3, await verify.Reviews.CountAsync(ct));
        Assert.Equal(3, await verify.Assignments.CountAsync(ct));
    }

    [Fact]
    public async Task BulkConcurrentRequestsCommitOnlyOneCompleteWorkflow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await ConfigureBulkAsync(fixture, [setup.Message], [1, 2, 3], AssignmentRetentionMode.PerConfirmation, ct);
        var gate = new SaveGate();
        await using var first = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        await using var second = new ORPDbContext(RetryOptions(fixture, new MeetBeforeSave(gate)));
        var responses = await Task.WhenAll(
            BulkHandler(first, setup.Actor).HandleAsync(new([setup.Message]), ct),
            BulkHandler(second, setup.Assignee).HandleAsync(new([setup.Message]), ct)).WaitAsync(TimeSpan.FromSeconds(45), ct);
        Assert.Equal(2, gate.Arrived);
        Assert.Equal(new[] { 200, 409 }, responses.Select(r => Assert.Single(r.Results).Status).Order());
        await using var verify = new ORPDbContext(fixture.Options);
        var message = await verify.Messages.SingleAsync(m => m.Id == setup.Message, ct);
        Assert.Equal(MessageState.Completed, message.State);
        Assert.Null(message.CurrentAssigneeId);
        var reviews = await verify.Reviews.OrderBy(r => r.Level).ToListAsync(ct);
        Assert.Equal(new[] { 1, 2, 3 }, reviews.Select(r => r.Level));
        Assert.Single(reviews.Select(r => r.ReviewerId).Distinct());
        Assert.Equal(3, await verify.AuditEvents.CountAsync(a => a.EventType == AuditEventType.ReviewApproved, ct));
        Assert.Equal(1, await verify.AuditEvents.CountAsync(a => a.EventType == AuditEventType.MessageCompleted, ct));
        Assert.False(await verify.Assignments.AnyAsync(a => a.EndedAt == null, ct));
    }

    [Fact]
    public async Task BulkLostCommitAcknowledgementReportsUnknownWithoutReplaying()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct, false);
        await ConfigureBulkAsync(fixture, [setup.Message], [1, 2], AssignmentRetentionMode.PerConfirmation, ct);
        var interceptor = new LoseBulkCommitAcknowledgement();
        await using (var db = new ORPDbContext(RetryOptions(fixture, interceptor)))
        {
            var result = Assert.Single((await BulkHandler(db, setup.Actor).HandleAsync(new([setup.Message]), ct)).Results);
            Assert.Equal(500, result.Status);
            Assert.Equal("commit_outcome_unknown", result.Error?.Code);
            Assert.Equal(1, interceptor.Commits);
        }
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(MessageState.Completed, (await verify.Messages.SingleAsync(m => m.Id == setup.Message, ct)).State);
        Assert.Equal(2, await verify.Reviews.CountAsync(ct));
        Assert.Equal(1, await verify.AuditEvents.CountAsync(a => a.EventType == AuditEventType.MessageCompleted, ct));
    }

    [Fact]
    public async Task OrdinaryHttpConfirmationReportsUnknownCommitWithoutReplay()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        var setup = await SeedAsync(fixture, ct);
        var interceptor = new LoseBulkCommitAcknowledgement();
        await using var factory = new CommitFailureFactory(fixture.ConnectionString, interceptor);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "theo.mercer");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "ordinary-unknown-commit");
        using var response = await client.PostAsJsonAsync($"/api/messages/{setup.Message}/reviews/confirm", new { level = 1 }, ct);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("commit_outcome_unknown", body.GetProperty("code").GetString());
        Assert.Equal("ordinary-unknown-commit", body.GetProperty("correlationId").GetString());
        Assert.Contains("Reload the message before retrying", body.GetProperty("detail").GetString());
        Assert.Equal(1, interceptor.Commits);
        await using var verify = new ORPDbContext(fixture.Options);
        Assert.Equal(1, await verify.Reviews.CountAsync(r => r.MessageId == setup.Message && r.Status == ReviewStatus.Approved, ct));
        Assert.Equal(1, await verify.AuditEvents.CountAsync(a => a.MessageId == setup.Message && a.EventType == AuditEventType.ReviewApproved, ct));
    }

    private sealed class CommitFailureFactory(string connection, IInterceptor interceptor) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection)
            .UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services => services.AddDbContext<ORPDbContext>(options => options.AddInterceptors(interceptor)));
    }

    [Fact]
    public async Task BulkPermissionMigrationGrantsExistingReviewerAndSupportsRollback()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await SeedAsync(fixture, ct, false);
        await using var db = new ORPDbContext(fixture.Options);
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, m => m.EndsWith("_AddBulkApprovePermission", StringComparison.Ordinal));
        Assert.True(index > 0);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[index - 1], ct);
        Assert.False(await db.Permissions.AnyAsync(p => p.Name == Permissions.ReviewBulkApprove, ct));
        await migrator.MigrateAsync(cancellationToken: ct);
        var roleId = await db.Roles.Where(r => r.Name == "Reviewer").Select(r => r.Id).SingleAsync(ct);
        Assert.True(await db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.Permission.Name == Permissions.ReviewBulkApprove, ct));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task ConfigureBulkAsync(SqlImportDatabase fixture, long[] ids, int[] levels,
        AssignmentRetentionMode retention, CancellationToken ct)
    {
        await using var db = new ORPDbContext(fixture.Options);
        var workflow = new WorkflowDefinition("BULK_TEST", "Bulk test");
        foreach (var level in levels) workflow.AddStep(level, level);
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, false, retention, false);
        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync(ct);
        foreach (var message in await db.Messages.Where(m => ids.Contains(m.Id)).ToListAsync(ct))
            message.ChangeWorkflow(workflow, []);
        await db.SaveChangesAsync(ct);
    }

    private static BulkApproveReviewsHandler BulkHandler(ORPDbContext db, int actor)
    {
        var log = new BusinessActionLog(NullLogger<ORPStore>.Instance);
        var store = new ORPStore(db, log);
        var user = new Actor(actor);
        var correlation = new Correlation();
        var assignments = new AssignmentCoordinator(store, new Clock());
        var transactions = new TransactionExecutor(db, log);
        var authorization = new MessageAuthorizationService(store, new UserAuthorizationQueries(db), user, correlation,
            NullLogger<MessageAuthorizationService>.Instance);
        return new(new BulkApproveReviewsValidator(), user, correlation, authorization, store, assignments,
            Handlers(db, actor).Confirm, transactions, NullLogger<BulkApproveReviewsHandler>.Instance);
    }

    private sealed class FailOnBulkSecondApproval(long messageId) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => eventData.Context!.ChangeTracker.Entries<Review>()
                .Any(e => e.Entity.MessageId == messageId && e.Entity.Level == 2 && e.Entity.Status == ReviewStatus.Approved)
                ? throw new IOException("Injected late bulk approval failure.") : ValueTask.FromResult(result);
    }

    private sealed class LoseBulkCommitAcknowledgement : DbTransactionInterceptor
    {
        public int Commits { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Commits++;
            throw new IOException("Injected lost commit acknowledgement.");
        }
    }
}
