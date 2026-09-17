using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class SqlServerHttpTransactionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task HttpFailureAfterSave_RollsBackReviewMessageAndAudit()
    {
        var saves = new ControlledSave();
        await using var factory = new MessageApiFactory(saves);
        await factory.InitializeAsync();
        using var client = Administrator(factory);
        var id = factory.MessageId(1);
        await AssignAsync(factory, client, id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var before = await db.AuditEvents.CountAsync(Ct);
        saves.FailNextSave = true;

        using var failed = await client.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new { level = 1 }, Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.True(saves.FailureInjected);
        Assert.Equal(MessageState.Assigned, await db.Messages.Where(m => m.Id == id).Select(m => m.State).SingleAsync(Ct));
        Assert.False(await db.Reviews.AnyAsync(r => r.MessageId == id, Ct));
        Assert.Equal(before, await db.AuditEvents.CountAsync(Ct));
        Assert.Single(await db.Assignments.Where(a => a.MessageId == id && a.EndedAt == null).ToArrayAsync(Ct));

        var reviewId = await StartAsync(client, id);
        Assert.Equal(reviewId, await db.Reviews.Where(r => r.MessageId == id).Select(r => r.Id).SingleAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.MessageId == id && a.EventType == AuditEventType.ReviewStarted, Ct));
    }

    [Fact]
    public async Task ConcurrentHttpStarts_WaitForCommit_AndReturnTheSameReview()
    {
        var saves = new ControlledSave();
        var reads = new MessageReadSignal();
        await using var factory = new MessageApiFactory(saves, reads);
        await factory.InitializeAsync();
        using var firstClient = Administrator(factory);
        using var secondClient = Administrator(factory);
        var id = factory.MessageId(1);
        await AssignAsync(factory, firstClient, id);
        saves.HoldNextSave = true;
        var first = StartAsync(firstClient, id);
        Task<long>? second = null;
        try
        {
            await saves.Saved.Task.WaitAsync(Timeout, Ct);
            await AssertMessageLockedAsync(factory, id);
            reads.Armed = true;
            second = StartAsync(secondClient, id);
            await reads.Entered.Task.WaitAsync(Timeout, Ct);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            saves.Release.TrySetResult();
            await first.WaitAsync(Timeout, Ct);
            if (second is not null) await second.WaitAsync(Timeout, Ct);
        }
        Assert.Equal(await first, await second!);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        Assert.Equal(await first, await db.Reviews.Where(r => r.MessageId == id).Select(r => r.Id).SingleAsync(Ct));
        Assert.Equal(MessageState.FirstReviewInProgress, await db.Messages.Where(m => m.Id == id).Select(m => m.State).SingleAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.MessageId == id && a.EventType == AuditEventType.ReviewStarted, Ct));
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.MessageId == id && a.EndedAt == null, Ct));
    }

    [Fact]
    public async Task ConcurrentHttpStarts_RetryARealSqlDeadlock_WithoutDuplicateReviews()
    {
        var reads = new OverlappingReads();
        var failures = new DeadlockFailures();
        await using var factory = new MessageApiFactory(reads, failures);
        await factory.InitializeAsync();
        using var firstClient = Administrator(factory);
        using var secondClient = Administrator(factory);
        var id = factory.MessageId(1);
        await AssignAsync(factory, firstClient, id);
        reads.Armed = true;

        // Both transactions retain shared locks before either attempts to update the message.
        // SQL Server must choose a deadlock victim when they upgrade those locks.
        var ids = await Task.WhenAll(StartAsync(firstClient, id), StartAsync(secondClient, id)).WaitAsync(Timeout, Ct);
        Assert.True(reads.Deadlocks + failures.Deadlocks > 0, "The scenario must observe SQL Server error 1205, not just run requests sequentially.");
        Assert.Equal(ids[0], ids[1]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        Assert.Equal(ids[0], await db.Reviews.Where(r => r.MessageId == id).Select(r => r.Id).SingleAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.MessageId == id && a.EventType == AuditEventType.ReviewStarted, Ct));
        Assert.Equal(1, await db.Assignments.CountAsync(a => a.MessageId == id && a.EndedAt == null, Ct));
        Assert.Equal(MessageState.FirstReviewInProgress, await db.Messages.Where(m => m.Id == id).Select(m => m.State).SingleAsync(Ct));
    }

    [Theory]
    [InlineData("approve", "reject", MessageState.Completed, ReviewStatus.Approved, AuditEventType.ReviewApproved)]
    [InlineData("reject", "approve", MessageState.Rejected, ReviewStatus.Rejected, AuditEventType.ReviewRejected)]
    public async Task ConcurrentHttpDecisions_CommitOneDecision_AndRejectTheStaleRequest(
        string firstAction, string secondAction, MessageState expectedState, ReviewStatus expectedReview, AuditEventType expectedEvent)
    {
        var saves = new ControlledSave();
        var reads = new MessageReadSignal();
        await using var factory = new MessageApiFactory(saves, reads);
        await factory.InitializeAsync();
        using var firstClient = Administrator(factory);
        using var secondClient = Administrator(factory);
        var id = factory.MessageId(1);
        await AssignAsync(factory, firstClient, id);
        var reviewId = await StartAsync(firstClient, id);
        var body = new { level = 1, reviewId, comment = "Concurrent decision" };
        saves.HoldNextSave = true;
        var first = firstClient.PostAsJsonAsync($"/api/messages/{id}/reviews/{firstAction}", body, Ct);
        Task<HttpResponseMessage>? second = null;
        try
        {
            await saves.Saved.Task.WaitAsync(Timeout, Ct);
            await AssertMessageLockedAsync(factory, id);
            reads.Armed = true;
            second = secondClient.PostAsJsonAsync($"/api/messages/{id}/reviews/{secondAction}", body, Ct);
            await reads.Entered.Task.WaitAsync(Timeout, Ct);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            saves.Release.TrySetResult();
            await first.WaitAsync(Timeout, Ct);
            if (second is not null) await second.WaitAsync(Timeout, Ct);
        }
        using var accepted = await first;
        using var rejected = await second!;
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("no longer active", await rejected.Content.ReadAsStringAsync(Ct));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        var message = await db.Messages.SingleAsync(m => m.Id == id, Ct);
        Assert.Equal(expectedState, message.State);
        Assert.Null(message.CurrentAssigneeId);
        Assert.Equal(expectedReview, (await db.Reviews.SingleAsync(r => r.MessageId == id, Ct)).Status);
        var decision = await db.AuditEvents.SingleAsync(a => a.MessageId == id &&
            (a.EventType == AuditEventType.ReviewApproved || a.EventType == AuditEventType.ReviewRejected), Ct);
        Assert.Equal(expectedEvent, decision.EventType);
        Assert.Equal(reviewId, decision.ReviewId);
        Assert.False(await db.Assignments.AnyAsync(a => a.MessageId == id && a.EndedAt == null, Ct));
    }

    private static HttpClient Administrator(MessageApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "admin");
        return client;
    }

    private static async Task AssignAsync(MessageApiFactory factory, HttpClient client, long id)
    {
        using var response = await client.PostAsJsonAsync($"/api/messages/{id}/assign", new { assignedTo = factory.UserId("admin") }, Ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<long> StartAsync(HttpClient client, long id)
    {
        using var response = await client.PostAsJsonAsync($"/api/messages/{id}/reviews/start", new { level = 1 }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StartReviewResponse>(Ct))!.ReviewId;
    }

    private static async Task AssertMessageLockedAsync(MessageApiFactory factory, long id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        // A separate SQL connection must actually encounter the uncommitted writer's lock.
        // READCOMMITTEDLOCK also works if the server uses read-committed snapshot isolation.
        command.CommandText = "SET LOCK_TIMEOUT 500; SELECT [State] FROM [orp].[Messages] WITH (READCOMMITTEDLOCK) WHERE [MessageId] = @id";
        command.Parameters.AddWithValue("@id", id);
        var blocked = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteScalarAsync(Ct));
        Assert.Equal(1222, blocked.Number);
    }

    private sealed class ControlledSave : SaveChangesInterceptor
    {
        public bool FailNextSave { get; set; }
        public bool FailureInjected { get; private set; }
        public bool HoldNextSave { get; set; }
        public TaskCompletionSource Saved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(IsolationLevel.Serializable, eventData.Context!.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel);
            if (FailNextSave)
            {
                FailNextSave = false;
                FailureInjected = true;
                throw new InvalidOperationException("Injected HTTP failure after SaveChanges");
            }
            if (HoldNextSave)
            {
                HoldNextSave = false;
                Saved.TrySetResult();
                await Release.Task.WaitAsync(Timeout, cancellationToken);
            }
            return result;
        }
    }

    private sealed class DeadlockFailures : SaveChangesInterceptor
    {
        private int deadlocks;
        public int Deadlocks => Volatile.Read(ref deadlocks);
        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Exception.GetBaseException() is SqlException { Number: 1205 }) Interlocked.Increment(ref deadlocks);
            return Task.CompletedTask;
        }
    }

    private sealed class OverlappingReads : DbCommandInterceptor
    {
        private int arrivals;
        private int deadlocks;
        private readonly TaskCompletionSource bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Armed { get; set; }
        public int Deadlocks => Volatile.Read(ref deadlocks);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.Transaction is not null && command.CommandText.Contains("FROM [orp].[Messages]", StringComparison.Ordinal))
            {
                Assert.Equal(IsolationLevel.Serializable, command.Transaction.IsolationLevel);
                var arrival = Interlocked.Increment(ref arrivals);
                if (arrival == 2) bothRead.TrySetResult();
                if (arrival <= 2) await bothRead.Task.WaitAsync(Timeout, cancellationToken);
            }
            return result;
        }

        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Exception is SqlException { Number: 1205 }) Interlocked.Increment(ref deadlocks);
            return Task.CompletedTask;
        }
    }

    private sealed class MessageReadSignal : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.Transaction is not null && command.CommandText.Contains("FROM [orp].[Messages]", StringComparison.Ordinal))
            {
                Armed = false;
                Assert.Equal(IsolationLevel.Serializable, command.Transaction.IsolationLevel);
                Entered.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
    }
}
