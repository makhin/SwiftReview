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
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ConfirmationUsesActualActorAndEndsAssignmentOnlyAtCompletion(int levels)
    {
        var f = new Fixture(levels);
        for (var level = 1; level <= levels; level++)
        {
            await f.Confirm.HandleAsync(1, new(level, f.Version, " confirmed "), CancellationToken.None);
            Assert.Equal(level == levels ? null : (int?)11, f.Message.CurrentAssigneeId);
        }
        Assert.Equal(MessageState.Completed, f.Message.State);
        Assert.All(f.Reviews, x => { Assert.Equal(22, x.ReviewerId); Assert.Equal("confirmed", x.Comment); });
        f.Store.Received(levels * 2 + 2).AddAudit(Arg.Any<AuditEvent>());
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
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Confirm.HandleAsync(1, new(1, f.Version), CancellationToken.None));
        f.Store.DidNotReceive().AddReview(Arg.Any<Review>());
    }

    [Fact]
    public async Task StaleVersionDoesNotCreateReviewOrAudit()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Confirm.HandleAsync(1,
            new(1, Convert.ToBase64String(new byte[8])), CancellationToken.None));
        f.Store.DidNotReceive().AddReview(Arg.Any<Review>());
        f.Store.DidNotReceive().AddAudit(Arg.Any<AuditEvent>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64")]
    [InlineData("AQ==")]
    public async Task InvalidVersionIsValidationError(string version)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ValidationException>(() => f.Confirm.HandleAsync(1, new(1, version), CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateConfirmCannotAdvanceAnotherLevel()
    {
        var f = new Fixture(2);
        await f.Confirm.HandleAsync(1, new(1, f.Version), CancellationToken.None);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Confirm.HandleAsync(1, new(1, f.Version), CancellationToken.None));
        Assert.Single(f.Reviews);
        Assert.Equal(MessageState.WaitingForSecondReview, f.Message.State);
    }

    [Fact]
    public async Task UndoByAnotherActorRetainsAssignmentAndCannotUndoFinalApproval()
    {
        var f = new Fixture(2);
        await f.Confirm.HandleAsync(1, new(1, f.Version), CancellationToken.None);
        f.Actor.UserId.Returns(33);
        f.Users.CheckAsync(33, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(false, true, true));
        f.Users.CanAccessWorkflowAsync(33, 0, Arg.Any<CancellationToken>()).Returns(true);
        await f.Undo.HandleAsync(1, new(f.Reviews[0].Id), CancellationToken.None);
        Assert.Equal(MessageState.Assigned, f.Message.State);
        Assert.Equal(11, f.Message.CurrentAssigneeId);
        Assert.Null(f.Assignment.EndedAt);
        await f.Confirm.HandleAsync(1, new(1, f.Version), CancellationToken.None);
        await f.Confirm.HandleAsync(1, new(2, f.Version), CancellationToken.None);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Undo.HandleAsync(1, new(f.Reviews[^1].Id), CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledUndoKeepsAdministratorException(bool administrator)
    {
        var f = new Fixture();
        await f.Confirm.HandleAsync(1, new(1, f.Version), CancellationToken.None);
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
            f.Assignments, new InlineTransactions(), f.Authorization);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => handler.HandleAsync(1, new(1), CancellationToken.None));
        Assert.Equal(MessageState.Assigned, f.Message.State);
    }

    [Fact]
    public async Task DisabledRejectionIsEnforcedForAdministratorInSessionMode()
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(0, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, ConfirmationActorMode.AnyAuthorizedUser,
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
        var workflow = (await f.Store.FindWorkflowAsync(0, CancellationToken.None))!;
        workflow.ConfigureConfirmation(ConfirmationMode.ReviewSession, ConfirmationActorMode.AnyAuthorizedUser,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => f.Confirm.HandleAsync(1, new(1, f.Version), CancellationToken.None));
        f.Store.DidNotReceive().AddReview(Arg.Any<Review>());
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task CapabilityUsesSameScopeOwnershipAndMode(bool viewInScope, bool actionInScope, bool expected)
    {
        var f = new Fixture();
        var workflow = (await f.Store.FindWorkflowAsync(0, CancellationToken.None))!;
        var access = new UserAccess(22, "actor", "Actor", false,
        [
            new(5, 7, [], viewInScope ? actionInScope ? [Permissions.MessageView, Permissions.ReviewLevel1] : [Permissions.MessageView]
                : actionInScope ? [Permissions.ReviewLevel1] : []),
            new(5, 8, [], [Permissions.MessageView, Permissions.ReviewLevel1])
        ]);
        var capabilities = ReviewCapabilities.For(f.Message, workflow, f.Reviews, 5, [7], access);
        Assert.Equal(expected, capabilities.CanConfirm);
        Assert.False(capabilities.CanReview);
        Assert.Equal(expected ? 1 : (int?)null, capabilities.ConfirmationLevel);
    }

    private sealed class Fixture
    {
        public IORPStore Store { get; } = Substitute.For<IORPStore>();
        public IUserAuthorizationQueries Users { get; } = Substitute.For<IUserAuthorizationQueries>();
        public ICurrentUser Actor { get; } = Substitute.For<ICurrentUser>();
        public IClock Clock { get; } = Substitute.For<IClock>();
        public ICorrelationContext Correlation { get; } = Substitute.For<ICorrelationContext>();
        public Message Message { get; } = new(1, 0);
        public List<Review> Reviews { get; } = [];
        public Assignment Assignment { get; } = new(1, 99, 11, DateTimeOffset.UtcNow);
        public string Version => Convert.ToBase64String(Message.RowVersion);
        public AssignmentCoordinator Assignments { get; }
        public MessageAuthorizationService Authorization { get; }
        public ConfirmReviewHandler Confirm { get; }
        public UndoReviewHandler Undo { get; }

        public Fixture(int levels = 1)
        {
            var workflow = new WorkflowDefinition(MessageDirection.Incoming, "Generic", "TYPE", 7);
            for (var level = 1; level <= levels; level++) workflow.AddStep(level, level);
            workflow.ConfigureConfirmation(ConfirmationMode.Immediate, ConfirmationActorMode.AnyAuthorizedUser,
                true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
            workflow.ConfigureUndo(levels == 1 ? UndoApprovalMode.Disabled : UndoApprovalMode.LatestNonFinal,
                UndoActorMode.AnyAuthorizedUser, UndoActiveReviewMode.Cancel);
            Message.Assign(11);
            typeof(Message).GetProperty(nameof(Message.RowVersion))!.SetValue(Message, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
            Actor.UserId.Returns(22);
            Clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            Correlation.CorrelationId.Returns("confirmation-test");
            Store.FindMessageAsync(1, Arg.Any<CancellationToken>()).Returns(Message);
            Store.FindWorkflowAsync(0, Arg.Any<CancellationToken>()).Returns(workflow);
            Store.FindMessageSourceAsync(1, Arg.Any<CancellationToken>()).Returns(new MessageSourceDto(1, "MSG", "TYPE", 5, [7], DateTimeOffset.UtcNow, "", "", MessageDirection.Incoming));
            Store.GetReviewsAsync(1, Arg.Any<CancellationToken>()).Returns(Reviews);
            Store.GetActiveAssignmentAsync(1, Arg.Any<CancellationToken>()).Returns(Assignment);
            Store.When(x => x.AddReview(Arg.Any<Review>())).Do(call =>
                typeof(Review).GetProperty(nameof(Review.Id))!.SetValue(call.Arg<Review>(), (long)Reviews.Count + 1));
            Users.CheckAsync(22, 5, Arg.Any<IReadOnlyList<int>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new UserPermissionCheck(false, true, true));
            Users.CanAccessWorkflowAsync(22, 0, Arg.Any<CancellationToken>()).Returns(true);
            Assignments = new AssignmentCoordinator(Store, Clock);
            Authorization = new MessageAuthorizationService(Store, Users, Actor, Correlation, NullLogger<MessageAuthorizationService>.Instance);
            Confirm = new(Store, new ConfirmReviewValidator(), Actor, Clock, Correlation, Assignments, new InlineTransactions(), Authorization);
            Undo = new(Store, new UndoReviewValidator(), Actor, Clock, Correlation, Assignments, new InlineTransactions(), Authorization);
        }
    }

    private sealed class InlineTransactions : ITransactionExecutor
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
    }
}
