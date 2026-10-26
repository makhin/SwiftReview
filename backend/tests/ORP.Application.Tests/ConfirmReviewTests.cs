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

public sealed class ConfirmReviewTests
{
    [Fact]
    public async Task AssigneeNeedsReviewPermissionButNotAssignmentPermissionToConfirm()
    {
        var f = new Fixture();
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Permissions.MessageAssign, Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(false, true, false));
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        Assert.Equal(MessageState.Completed, f.Message.State);
    }

    [Fact]
    public async Task UnassignedImmediateCapabilitiesExposeEachCurrentLevelAndClearAfterCompletion()
    {
        var f = new Fixture(3);
        f.Message.Unassign();
        f.Assignment.End(f.Clock.UtcNow);
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, true, AssignmentRetentionMode.PerConfirmation, false);
        var access = new UserAccess(22, "actor", "Actor", false,
            [new(5, 7, [], [Permissions.MessageView, Permissions.MessageAssign,
                Permissions.ReviewLevel1, Permissions.ReviewLevel2, Permissions.ReviewLevel3])]);
        for (var level = 1; level <= 3; level++)
        {
            Assert.Null(f.Message.CurrentAssigneeId);
            var actions = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access);
            Assert.Equal(ConfirmationMode.Immediate, actions.ConfirmationMode);
            Assert.True(actions.CanConfirm);
            Assert.Equal(level, actions.ConfirmationLevel);
            Assert.False(actions.CanReview);
            await f.Confirm.HandleAsync(1, new(level), CancellationToken.None);
        }
        var completed = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access);
        Assert.False(completed.CanConfirm);
        Assert.Null(completed.ConfirmationLevel);
        Assert.False(completed.CanReview);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionCapabilitiesNeverExposeImmediateConfirmation(bool assigned)
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, true, AssignmentRetentionMode.PerConfirmation, false);
        if (!assigned) f.Message.Unassign();
        var access = new UserAccess(22, "actor", "Actor", false,
            [new(5, 7, [], [Permissions.MessageView, Permissions.MessageAssign, Permissions.ReviewLevel1])]);
        var actions = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access);
        Assert.False(actions.CanConfirm);
        Assert.Null(actions.ConfirmationLevel);
        Assert.Equal(assigned, actions.CanReview);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnassignedCapabilityRespectsFutureAssignmentAndReviewerReuse(bool reuse)
    {
        var f = new Fixture(2);
        f.Message.Unassign();
        f.Assignment.End(f.Clock.UtcNow);
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, reuse, AssignmentRetentionMode.PerConfirmation, false);
        var future = new Assignment(1, 99, 22, f.Clock.UtcNow, 2);
        f.AssignmentHistory.Add(future);
        var access = new UserAccess(22, "actor", "Actor", false,
            [new(5, 7, [], [Permissions.MessageView, Permissions.ReviewLevel1, Permissions.MessageAssign])]);
        Assert.Equal(reuse, ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access, [future]).CanConfirm);
        if (reuse)
            await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        else
        {
            await Assert.ThrowsAsync<ValidationException>(() => f.Confirm.HandleAsync(1, new(1), CancellationToken.None));
            Assert.Null(f.Message.CurrentAssigneeId);
            Assert.Empty(f.Reviews);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnassignedConfirmationRequiresAssignmentPermission(bool canAssign)
    {
        var f = new Fixture();
        f.Message.Unassign();
        f.Assignment.End(f.Clock.UtcNow);
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Permissions.MessageAssign, Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(false, true, canAssign));
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        var permissions = new List<string> { Permissions.MessageView, Permissions.ReviewLevel1 };
        if (canAssign) permissions.Add(Permissions.MessageAssign);
        var access = new UserAccess(22, "actor", "Actor", false, [new(5, 7, [], permissions)]);
        var actions = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access);
        Assert.Equal(canAssign, actions.CanConfirm);
        Assert.Equal(canAssign ? 1 : (int?)null, actions.ConfirmationLevel);
        Assert.False(actions.CanReview);
        if (canAssign)
        {
            await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
            Assert.Equal(MessageState.Completed, f.Message.State);
            Assert.Equal(22, Assert.Single(f.Reviews).ReviewerId);
            f.Store.Received(1).AddAssignment(Arg.Any<Assignment>());
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Confirm.HandleAsync(1, new(1), CancellationToken.None));
            Assert.Null(f.Message.CurrentAssigneeId);
            f.Store.DidNotReceive().AddAssignment(Arg.Any<Assignment>());
            Assert.Empty(f.Reviews);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReassignmentRevokesConfirmationWithoutClientVersion(bool administrator)
    {
        var f = new Fixture();
        f.Message.Assign(33);
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(administrator, true, true));
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        var access = new UserAccess(22, "actor", "Actor", administrator,
            [new(5, 7, [], [Permissions.MessageView, Permissions.ReviewLevel1])]);
        var actions = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access);
        Assert.False(actions.CanConfirm);
        Assert.Null(actions.ConfirmationLevel);
        Assert.False(actions.CanReview);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Confirm.HandleAsync(1, new(1), CancellationToken.None));
        f.Store.DidNotReceive().AddReview(Arg.Any<Review>());
        f.Store.DidNotReceive().AddAudit(Arg.Any<AuditEvent>());
        Assert.Equal(33, f.Message.CurrentAssigneeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionStartRequiresAssignmentAndNeverAutoAssigns(bool administrator)
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, true,
            AssignmentRetentionMode.UntilWorkflowCompleted, false);
        f.Message.Assign(33);
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(administrator, true, true));
        var handler = new StartReviewHandler(f.Store, new StartReviewValidator(), f.Actor, f.Clock,
            f.Correlation, new InlineTransactions(), f.Authorization);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.HandleAsync(1, new(1), CancellationToken.None));
        Assert.Equal(33, f.Message.CurrentAssigneeId);
        Assert.Empty(f.Reviews);
        f.Store.DidNotReceive().AddAssignment(Arg.Any<Assignment>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionApprovalRequiresAssignedReviewAuthor(bool administrator)
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, true,
            AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var review = f.Message.StartReview(1, 22, workflow, f.Reviews, f.Clock.UtcNow);
        typeof(Review).GetProperty(nameof(Review.Id))!.SetValue(review, 1L);
        f.Reviews.Add(review);
        f.Actor.UserId.Returns(33);
        f.Users.CheckAsync(33, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(administrator, true, true));
        var handler = new ApproveReviewHandler(f.Store, new ApproveReviewValidator(), f.Actor, f.Clock,
            f.Correlation, f.Assignments, new InlineTransactions(), f.Authorization);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.HandleAsync(1, new(1, review.Id, null), CancellationToken.None));
        Assert.Equal(ReviewStatus.InProgress, review.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ConfirmationUsesActualActorAndEndsAssignmentOnlyAtCompletion(int levels)
    {
        var f = new Fixture(levels);
        for (var level = 1; level <= levels; level++)
        {
            await f.Confirm.HandleAsync(1, new(level, " confirmed "), CancellationToken.None);
            Assert.Equal(level == levels ? null : (int?)22, f.Message.CurrentAssigneeId);
        }
        Assert.Equal(MessageState.Completed, f.Message.State);
        Assert.All(f.Reviews, x => { Assert.Equal(22, x.ReviewerId); Assert.Equal("confirmed", x.Comment); });
        f.Store.Received(levels * 4).AddAudit(Arg.Any<AuditEvent>());
        Assert.NotNull(f.Assignment.EndedAt);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MissingScopedAccessOrPermissionIsRejected(bool view, bool permission)
    {
        var f = new Fixture();
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Permissions.ReviewLevel1, Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(false, view, permission));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Confirm.HandleAsync(1, new(1), CancellationToken.None));
        f.Store.DidNotReceive().AddReview(Arg.Any<Review>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task InvalidLevelIsValidationError(int level)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ValidationException>(() => f.Confirm.HandleAsync(1, new(level), CancellationToken.None));
        f.Store.DidNotReceive().AddReview(Arg.Any<Review>());
    }

    [Fact]
    public async Task DuplicateConfirmCannotAdvanceAnotherLevel()
    {
        var f = new Fixture(2);
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Confirm.HandleAsync(1, new(1), CancellationToken.None));
        Assert.Single(f.Reviews);
        Assert.Equal(MessageState.WaitingForSecondReview, f.Message.State);
    }

    [Fact]
    public async Task UndoByAnotherActorRetainsAssignmentAndCannotUndoFinalApproval()
    {
        var f = new Fixture(2);
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        f.Actor.UserId.Returns(33);
        f.Users.CheckAsync(33, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(false, true, true));
        await f.Undo.HandleAsync(1, new(f.Reviews[0].Id), CancellationToken.None);
        Assert.Equal(MessageState.Assigned, f.Message.State);
        Assert.Equal(22, f.Message.CurrentAssigneeId);
        Assert.NotNull(f.Assignment.EndedAt);
        Assert.Contains(f.AssignmentHistory, a => a.Level == 1 && a.AssignedTo == 22 && a.EndedAt is null);
        f.Actor.UserId.Returns(22);
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        await f.Confirm.HandleAsync(1, new(2), CancellationToken.None);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Undo.HandleAsync(1, new(f.Reviews[^1].Id), CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledUndoKeepsAdministratorException(bool administrator)
    {
        var f = new Fixture();
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Permissions.ReviewUndo, Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(administrator, true, true));
        if (administrator)
        {
            await f.Undo.HandleAsync(1, new(f.Reviews[0].Id), CancellationToken.None);
            Assert.Equal(MessageState.Assigned, f.Message.State);
        }
        else await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Undo.HandleAsync(1, new(f.Reviews[0].Id), CancellationToken.None));
    }

    [Fact]
    public async Task ImmediateWorkflowRejectsLegacyStartEvenForAdministrator()
    {
        var f = new Fixture();
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(true, true, true));
        var handler = new StartReviewHandler(f.Store, new StartReviewValidator(), f.Actor, f.Clock, f.Correlation,
            new InlineTransactions(), f.Authorization);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => handler.HandleAsync(1, new(1), CancellationToken.None));
        Assert.Equal(MessageState.Assigned, f.Message.State);
    }

    [Fact]
    public async Task DisabledRejectionIsEnforcedForAdministratorInSessionMode()
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var review = f.Message.StartReview(1, 22, workflow, f.Reviews, DateTimeOffset.UtcNow);
        typeof(Review).GetProperty(nameof(Review.Id))!.SetValue(review, 1L);
        f.Reviews.Add(review);
        f.Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(true, true, true));
        var handler = new RejectReviewHandler(f.Store, new RejectReviewValidator(), f.Actor, f.Clock, f.Correlation,
            f.Assignments, new InlineTransactions(), f.Authorization);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => handler.HandleAsync(1, new(1, review.Id, null), CancellationToken.None));
        Assert.Equal(ReviewStatus.InProgress, review.Status);
    }

    [Fact]
    public async Task SessionWorkflowCannotUseImmediateConfirm()
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Confirm.HandleAsync(1, new(1), CancellationToken.None));
        f.Store.DidNotReceive().AddReview(Arg.Any<Review>());
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task CapabilityUsesSameScopeOwnershipAndMode(bool viewInScope, bool actionInScope, bool expected)
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        var access = new UserAccess(22, "actor", "Actor", false,
        [
            new(5, 7, [], viewInScope ? actionInScope ? [Permissions.MessageView, Permissions.ReviewLevel1] : [Permissions.MessageView]
                : actionInScope ? [Permissions.ReviewLevel1] : []),
            new(5, 8, [], [Permissions.MessageView, Permissions.ReviewLevel1])
        ]);
        var capabilities = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access);
        Assert.Equal(expected, capabilities.CanConfirm);
        Assert.False(capabilities.CanReview);
        Assert.Equal(expected ? 1 : (int?)null, capabilities.ConfirmationLevel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmLoadsOneContextAndPreservesSeparateSaves(bool assigned)
    {
        var f = new Fixture();
        if (!assigned)
        {
            f.Message.Unassign();
            f.Assignment.End(f.Clock.UtcNow);
        }
        f.Store.ClearReceivedCalls();
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        Assert.Equal(MessageState.Completed, f.Message.State);
        Assert.Single(f.Reviews);
        Assert.All(f.AssignmentHistory, a => Assert.NotNull(a.EndedAt));
        await f.Store.Received(1).FindMessageAsync(1, Arg.Any<CancellationToken>());
        await f.Store.Received(1).FindMessageSourceAsync(1, Arg.Any<CancellationToken>());
        await f.Store.Received(1).GetReviewsAsync(1, Arg.Any<CancellationToken>());
        await f.Store.Received(1).FindWorkflowAsync(1, Arg.Any<CancellationToken>());
        await f.Store.Received(1).GetActiveAssignmentsAsync(1, Arg.Any<CancellationToken>());
        await f.Store.Received(assigned ? 1 : 2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.Users.Received(assigned ? 0 : 1).CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Permissions.MessageAssign, Arg.Any<CancellationToken>());
        await f.Users.Received().CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Permissions.ReviewLevel1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidSessionLevelDoesNotStartTransactionOrReadData()
    {
        var f = new Fixture();
        var transactions = Substitute.For<ITransactionExecutor>();
        var handler = new StartReviewHandler(f.Store, new StartReviewValidator(), f.Actor, f.Clock,
            f.Correlation, transactions, f.Authorization);
        f.Store.ClearReceivedCalls();
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1, new(4), CancellationToken.None));
        Assert.Empty(transactions.ReceivedCalls());
        Assert.Empty(f.Store.ReceivedCalls());
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task UndoCapabilityRequiresViewAndUndoInTheSameProcessingScope(bool viewInScope, bool undoInScope, bool expected)
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(1, CancellationToken.None))!;
        workflow.ConfigureUndo(UndoApprovalMode.Latest, UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
        await f.Confirm.HandleAsync(1, new(1), CancellationToken.None);
        var access = new UserAccess(22, "actor", "Actor", false,
        [
            new(5, 7, [], viewInScope ? undoInScope ? [Permissions.MessageView, Permissions.ReviewUndo] : [Permissions.MessageView]
                : undoInScope ? [Permissions.ReviewUndo] : []),
            new(5, 8, [], [Permissions.MessageView, Permissions.ReviewUndo]),
            new(6, 7, [], [Permissions.MessageView, Permissions.ReviewUndo])
        ]);
        var capabilities = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, access);
        Assert.Equal(expected ? f.Reviews[0].Id : (long?)null, capabilities.UndoReviewId);
        Assert.Equal(ReviewStatus.Approved, f.Reviews[0].Status);
        Assert.Equal(MessageState.Completed, f.Message.State);
    }

    private sealed class Fixture
    {
        public IORPStore Store { get; } = Substitute.For<IORPStore>();
        public IUserAuthorizationQueries Users { get; } = Substitute.For<IUserAuthorizationQueries>();
        public ICurrentUser Actor { get; } = Substitute.For<ICurrentUser>();
        public IClock Clock { get; } = Substitute.For<IClock>();
        public ICorrelationContext Correlation { get; } = Substitute.For<ICorrelationContext>();
        public Message Message { get; } = new(1, 1, 5, 7, DateTimeOffset.UtcNow);
        public List<Review> Reviews { get; } = [];
        public Assignment Assignment { get; } = new(1, 99, 22, DateTimeOffset.UtcNow);
        public List<Assignment> AssignmentHistory { get; } = [];
        public AssignmentCoordinator Assignments { get; }
        public MessageAuthorizationService Authorization { get; }
        public ConfirmReviewHandler Confirm { get; }
        public UndoReviewHandler Undo { get; }

        public Fixture(int levels = 1)
        {
            var workflow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Generic");
            typeof(WorkflowDefinition).GetProperty(nameof(WorkflowDefinition.Id))!.SetValue(workflow, 1);
            for (var level = 1; level <= levels; level++) workflow.AddStep(level, level);
            workflow.ConfigureConfirmation(ConfirmationMode.Immediate,
                true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
            workflow.ConfigureUndo(levels == 1 ? UndoApprovalMode.Disabled : UndoApprovalMode.LatestNonFinal,
                UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
            Message.Assign(22);
            Actor.UserId.Returns(22);
            Clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            Correlation.CorrelationId.Returns("confirmation-test");
            Store.FindMessageAsync(1, Arg.Any<CancellationToken>()).Returns(Message);
            Store.FindWorkflowAsync(1, Arg.Any<CancellationToken>()).Returns(workflow);
            Store.FindMessageSourceAsync(1, Arg.Any<CancellationToken>()).Returns(new MessageSourceDto(1, "MSG", "TYPE", 5, [7], DateTimeOffset.UtcNow, "", "", MessageDirection.Incoming));
            Store.GetReviewsAsync(1, Arg.Any<CancellationToken>()).Returns(Reviews);
            AssignmentHistory.Add(Assignment);
            Store.GetActiveAssignmentsAsync(1, Arg.Any<CancellationToken>()).Returns(_ => AssignmentHistory.Where(a => a.EndedAt is null).ToList());
            Store.When(x => x.AddAssignment(Arg.Any<Assignment>())).Do(call => AssignmentHistory.Add(call.Arg<Assignment>()));
            Store.When(x => x.AddReview(Arg.Any<Review>())).Do(call =>
                typeof(Review).GetProperty(nameof(Review.Id))!.SetValue(call.Arg<Review>(), (long)Reviews.Count + 1));
            Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new UserPermissionCheck(false, true, true));
            Assignments = new AssignmentCoordinator(Store, Clock);
            Authorization = new MessageAuthorizationService(Store, Users, Actor, Correlation, NullLogger<MessageAuthorizationService>.Instance);
            var assign = new ORP.Application.Assignments.Assign.AssignMessageHandler(Store, Users,
                new ORP.Application.Assignments.Assign.AssignMessageValidator(), Actor, Correlation, Assignments,
                new InlineTransactions(), Authorization);
            Confirm = new(Store, new ConfirmReviewValidator(), Actor, Clock, Correlation, Assignments, new InlineTransactions(), Authorization, assign);
            Undo = new(Store, new UndoReviewValidator(), Actor, Clock, Correlation, Assignments, new InlineTransactions(), Authorization);
        }
    }

    private sealed class InlineTransactions : ITransactionExecutor
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
    }
}
