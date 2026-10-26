using System.Diagnostics.Metrics;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Authorization;
using ORP.Application.Reviews;
using ORP.Domain.Assignments;
using ORP.Domain.Auditing;
using ORP.Domain.Common;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Application.Tests;

public sealed class BulkApproveReviewsTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task ApprovesRequiredLevelsAndAuditsRealActor(int levels, bool skipSecond)
    {
        var f = new Fixture(levels, skipSecond);
        var result = Assert.Single((await f.Bulk.HandleAsync(new([1], " checked "), CancellationToken.None)).Results);
        Assert.Equal(200, result.Status);
        Assert.Equal(f.Workflow.RequiredLevels(), result.ApprovedLevels);
        Assert.Equal(MessageState.Completed, f.Message.State);
        Assert.False(f.Workflow.AllowReviewerReuse);
        Assert.Null(f.Message.CurrentAssigneeId);
        Assert.All(f.Reviews, r => { Assert.Equal(22, r.ReviewerId); Assert.Equal("checked", r.Comment); });
        Assert.All(f.History, a => Assert.NotNull(a.EndedAt));
        Assert.Equal(f.Workflow.RequiredLevels().Count, f.Audit.Count(a => a.EventType == AuditEventType.ReviewApproved));
        Assert.Single(f.Audit, a => a.EventType == AuditEventType.MessageCompleted);
        Assert.All(f.Audit, a => { Assert.Equal(22, a.UserId); Assert.Equal("bulk-unit", a.CorrelationId); });
    }

    [Fact]
    public async Task OrdinaryConfirmationStillRejectsReuseButBulkCompletesRemainingLevels()
    {
        var f = new Fixture(3);
        f.Message.Assign(22);
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        f.Message.Assign(22);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Confirm.HandleAsync(1, new(2), CancellationToken.None));
        var original = Assert.Single(f.Reviews);
        var result = Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results);
        Assert.Equal(new[] { 2, 3 }, result.ApprovedLevels);
        Assert.Same(original, f.Reviews[0]);
        Assert.Equal(3, f.Reviews.Count);
    }

    [Theory]
    [InlineData(Permissions.ReviewBulkApprove, true, false)]
    [InlineData(Permissions.MessageAssign, true, false)]
    [InlineData(Permissions.ReviewLevel3, true, false)]
    [InlineData(Permissions.ReviewBulkApprove, false, true)]
    [InlineData(Permissions.ReviewLevel3, false, true)]
    public async Task ChecksEveryPermissionAndScopeBeforeMutation(string permission, bool view, bool permitted)
    {
        var f = new Fixture(3);
        f.Users.GetPermissionsAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionSet(false, Permissions.All.Where(p =>
                (view || p != Permissions.MessageView) && (permitted || p != permission)).ToArray()));
        var result = Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results);
        Assert.Equal(403, result.Status);
        Assert.Equal("forbidden", result.Error?.Code);
        Assert.Empty(f.Reviews);
        Assert.Empty(f.History);
        Assert.Empty(f.Audit);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task NeverTakesAnotherReviewersAssignment(int level)
    {
        var f = new Fixture(3);
        f.History.Add(new(1, 33, 33, DateTimeOffset.UtcNow, level));
        if (level == 1) f.Message.Assign(33);
        var result = Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results);
        Assert.Equal(409, result.Status);
        Assert.Null(Assert.Single(f.History).EndedAt);
        Assert.Empty(f.Reviews);
        Assert.Empty(f.Audit);
    }

    [Theory]
    [InlineData(MessageState.Completed)]
    [InlineData(MessageState.Rejected)]
    [InlineData(MessageState.ViewOnly)]
    [InlineData(MessageState.FirstReviewInProgress)]
    public async Task RejectsUnavailableStates(MessageState state)
    {
        var f = new Fixture();
        typeof(Message).GetProperty(nameof(Message.State))!.SetValue(f.Message, state);
        var result = Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results);
        Assert.Equal(409, result.Status);
        Assert.Empty(f.Reviews);
    }

    [Theory]
    [InlineData(false, ConfirmationMode.Immediate)]
    [InlineData(true, ConfirmationMode.ReviewSession)]
    [InlineData(true, ConfirmationMode.None)]
    public async Task RequiresActiveImmediateWorkflow(bool active, ConfirmationMode mode)
    {
        var f = new Fixture();
        f.Workflow.ConfigureConfirmation(mode, false, AssignmentRetentionMode.PerConfirmation, false);
        if (!active) f.Workflow.Deactivate();
        Assert.Equal(409, Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results).Status);
        Assert.Empty(f.History);
    }

    [Fact]
    public async Task ValidatesWholeRequestBeforeReadingMessages()
    {
        var f = new Fixture();
        BulkApproveReviewsRequest[] invalid = [new(null!), new([]), new([0]), new([-1]), new([1, 1]),
            new(Enumerable.Range(1, 101).Select(i => (long)i).ToArray()), new([1], new string('x', 2001))];
        foreach (var request in invalid)
            await Assert.ThrowsAsync<ValidationException>(() => f.Bulk.HandleAsync(request, CancellationToken.None));
        Assert.Empty(f.Store.ReceivedCalls());
    }

    [Fact]
    public async Task ResultsPreserveInputOrderAndCompletedRetriesDoNotCreateReviews()
    {
        var f = new Fixture();
        var response = await f.Bulk.HandleAsync(new([99, 1, 98]), CancellationToken.None);
        Assert.Equal(new long[] { 99, 1, 98 }, response.Results.Select(r => r.MessageId));
        Assert.Equal(new[] { 404, 200, 404 }, response.Results.Select(r => r.Status));
        Assert.Equal(409, Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results).Status);
        Assert.Single(f.Reviews);
    }

    [Fact]
    public async Task UnknownCommitIsExplicitAndNotRetried()
    {
        var f = new Fixture();
        var transactions = Substitute.For<ITransactionExecutor>();
        transactions.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<BulkApproveMessageResult>>>(), Arg.Any<CancellationToken>())
            .Returns<Task<BulkApproveMessageResult>>(_ => throw new TransactionCommitOutcomeUnknownException());
        var result = Assert.Single((await f.CreateBulk(transactions).HandleAsync(new([1]), CancellationToken.None)).Results);
        Assert.Equal(500, result.Status);
        Assert.Equal("commit_outcome_unknown", result.Error?.Code);
        Assert.Single(transactions.ReceivedCalls());
    }

    [Fact]
    public async Task RequestCancellationStopsBatch()
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Bulk.HandleAsync(new([1]), cancellation.Token));
        Assert.Empty(f.Reviews);
    }

    [Fact]
    public async Task PermissionsAreReadOncePerMessageAndReloadedOnNextRequest()
    {
        var f = new Fixture(3);
        Assert.Equal(200, Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results).Status);
        await f.Users.Received(1).GetPermissionsAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(f.Users.ReceivedCalls(), call => call.GetMethodInfo().Name == nameof(IUserAuthorizationQueries.CheckAsync));
        f.Users.GetPermissionsAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionSet(false, []));
        Assert.Equal(403, Assert.Single((await f.Bulk.HandleAsync(new([1]), CancellationToken.None)).Results).Status);
    }

    [Fact]
    public async Task MetricsCountFinalMessageOutcomesAndCancelledBatchDuration()
    {
        var active = new AsyncLocal<bool> { Value = true };
        var messages = new List<string>();
        var batches = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == BulkApprovalMetrics.MeterName) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (!active.Value) return;
            Assert.Equal("orp.bulk_approve.messages", instrument.Name);
            Assert.Equal(1, value);
            Assert.Equal(1, tags.Length);
            messages.Add((string)tags[0].Value!);
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (!active.Value) return;
            Assert.Equal("orp.bulk_approve.batch.duration", instrument.Name);
            Assert.True(value >= 0);
            Assert.Equal(1, tags.Length);
            batches.Add((string)tags[0].Value!);
        });
        listener.Start();
        var f = new Fixture();
        await f.Bulk.HandleAsync(new([1, 99]), CancellationToken.None);
        var unknown = Substitute.For<ITransactionExecutor>();
        unknown.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<BulkApproveMessageResult>>>(), Arg.Any<CancellationToken>())
            .Returns<Task<BulkApproveMessageResult>>(_ => throw new TransactionCommitOutcomeUnknownException());
        await f.CreateBulk(unknown).HandleAsync(new([1]), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancel = Substitute.For<ITransactionExecutor>();
        var attempted = 0;
        cancel.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<BulkApproveMessageResult>>>(), Arg.Any<CancellationToken>())
            .Returns<Task<BulkApproveMessageResult>>(_ =>
            {
                if (++attempted == 1) return Task.FromResult(new BulkApproveMessageResult(1, 200));
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            f.CreateBulk(cancel).HandleAsync(new([1, 2]), cancellation.Token));
        Assert.Equal(new[] { "success", "not_found", "commit_outcome_unknown", "success" }, messages);
        Assert.Equal(new[] { "completed", "completed", "cancelled" }, batches);
    }

    private sealed class Fixture
    {
        public IORPStore Store { get; } = Substitute.For<IORPStore>();
        public IUserAuthorizationQueries Users { get; } = Substitute.For<IUserAuthorizationQueries>();
        private ICurrentUser Actor { get; } = Substitute.For<ICurrentUser>();
        private ICorrelationContext Correlation { get; } = Substitute.For<ICorrelationContext>();
        public Message Message { get; } = new(1, 1, 5, 7, DateTimeOffset.UtcNow);
        public WorkflowDefinition Workflow { get; } = new("BULK", "Bulk");
        public List<Review> Reviews { get; } = [];
        public List<Assignment> History { get; } = [];
        public List<AuditEvent> Audit { get; } = [];
        private AssignmentCoordinator Assignments { get; }
        private MessageAuthorizationService Authorization { get; }
        public ConfirmReviewHandler Confirm { get; }
        public BulkApproveReviewsHandler Bulk => CreateBulk(new InlineTransactions());

        public Fixture(int levels = 1, bool skipSecond = false)
        {
            typeof(WorkflowDefinition).GetProperty(nameof(WorkflowDefinition.Id))!.SetValue(Workflow, 1);
            for (var level = 1; level <= levels; level++)
                if (!skipSecond || level != 2) Workflow.AddStep(level, level);
            Workflow.ConfigureConfirmation(ConfirmationMode.Immediate, false, AssignmentRetentionMode.PerConfirmation, false);
            Actor.UserId.Returns(22);
            Correlation.CorrelationId.Returns("bulk-unit");
            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            Store.FindMessageAsync(1, Arg.Any<CancellationToken>()).Returns(Message);
            Store.FindMessageSourceAsync(1, Arg.Any<CancellationToken>()).Returns(new MessageSourceDto(1, "MSG", "TYPE", 5, [7], DateTimeOffset.UtcNow, "", "", MessageDirection.Incoming));
            Store.FindWorkflowAsync(1, Arg.Any<CancellationToken>()).Returns(Workflow);
            Store.GetReviewsAsync(1, Arg.Any<CancellationToken>()).Returns(Reviews);
            Store.GetActiveAssignmentsAsync(1, Arg.Any<CancellationToken>()).Returns(_ => History.Where(a => a.EndedAt is null).ToList());
            Store.When(s => s.AddAssignment(Arg.Any<Assignment>())).Do(c => History.Add(c.Arg<Assignment>()));
            Store.When(s => s.AddAudit(Arg.Any<AuditEvent>())).Do(c => Audit.Add(c.Arg<AuditEvent>()));
            Users.CheckAsync(22, 5, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 7 })), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new UserPermissionCheck(false, true, true));
            Users.GetPermissionsAsync(22, 5, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 7 })), Arg.Any<CancellationToken>())
                .Returns(new UserPermissionSet(false, Permissions.All));
            Assignments = new(Store, clock);
            Authorization = new(Store, Users, Actor, Correlation, NullLogger<MessageAuthorizationService>.Instance);
            var assign = new ORP.Application.Assignments.Assign.AssignMessageHandler(Store, Users,
                new ORP.Application.Assignments.Assign.AssignMessageValidator(), Actor, Correlation, Assignments,
                new InlineTransactions(), Authorization);
            Confirm = new(Store, new ConfirmReviewValidator(), Actor, clock, Correlation, Assignments, new InlineTransactions(), Authorization, assign);
        }

        public BulkApproveReviewsHandler CreateBulk(ITransactionExecutor transactions) => new(new BulkApproveReviewsValidator(),
            Actor, Correlation, Authorization, Store, Assignments, Confirm, transactions, NullLogger<BulkApproveReviewsHandler>.Instance);
    }

    private sealed class InlineTransactions : ITransactionExecutor
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
    }
}
