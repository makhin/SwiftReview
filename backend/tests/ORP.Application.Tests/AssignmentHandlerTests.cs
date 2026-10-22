using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ORP.Application.Authorization;
using FluentValidation;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Assignments;
using ORP.Application.Assignments.Assign;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Reviews;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Application.Tests;

public sealed class AssignmentHandlerTests
{
    [Fact]
    public async Task AssigneeOutsideMessageScope_IsRejected()
    {
        var store = Substitute.For<IORPStore>();
        store.GetActiveAssignmentsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns([]);
        store.FindWorkflowAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Default").AddStep(1, 1).AddStep(2, 2).AddStep(3, 3));
        var access = Substitute.For<IUserAuthorizationQueries>();
        var user = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        var message = new Message(1, 1, 1, 1, DateTimeOffset.UtcNow);
        store.FindMessageAsync(1, Arg.Any<CancellationToken>()).Returns(message);
        store.GetReviewsAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        store.FindMessageSourceAsync(1, Arg.Any<CancellationToken>()).Returns(
            new MessageSourceDto(1, "EXT-ASSIGN", "MT199", 1, [1], DateTimeOffset.UtcNow,
                "A", "B", MessageDirection.Incoming));
        access.CheckAsync(2, 1, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })), Permissions.ReviewLevel1, Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, false, false));
        user.UserId.Returns(5);
        access.CheckAsync(5, 1, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })), Permissions.MessageAssign, Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, true, true));

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1,
            new AssignMessageRequest(2), CancellationToken.None));
        store.DidNotReceive().AddAudit(Arg.Any<AuditEvent>());
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessfulAssignment_WritesCompleteAuditEvent()
    {
        var store = Substitute.For<IORPStore>();
        store.GetActiveAssignmentsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns([]);
        store.FindWorkflowAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Default").AddStep(1, 1).AddStep(2, 2).AddStep(3, 3));
        var access = Substitute.For<IUserAuthorizationQueries>();
        var user = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        var message = new Message(1, 1, 1, 1, DateTimeOffset.UtcNow);
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        AuditEvent? audit = null;
        store.FindMessageAsync(1, Arg.Any<CancellationToken>()).Returns(message);
        store.GetReviewsAsync(1, Arg.Any<CancellationToken>()).Returns([]);
        store.FindMessageSourceAsync(1, Arg.Any<CancellationToken>()).Returns(
            new MessageSourceDto(1, "EXT-ASSIGN", "MT199", 1, [1], now, "A", "B", MessageDirection.Incoming));
        access.CheckAsync(2, 1, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })), Permissions.ReviewLevel1, Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, true, true));
        user.UserId.Returns(5);
        access.CheckAsync(5, 1, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })), Permissions.MessageAssign, Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, true, true));
        clock.UtcNow.Returns(now);
        correlation.CorrelationId.Returns("assign-correlation");
        store.When(x => x.AddAudit(Arg.Any<AuditEvent>())).Do(x => audit = x.Arg<AuditEvent>());

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));
        await handler.HandleAsync(1, new AssignMessageRequest(2), TestContext.Current.CancellationToken);

        await store.Received(1).FindMessageAsync(1, Arg.Any<CancellationToken>());
        await store.Received(1).FindMessageSourceAsync(1, Arg.Any<CancellationToken>());
        await store.Received(1).GetReviewsAsync(1, Arg.Any<CancellationToken>());
        Assert.NotNull(audit);
        Assert.Equal(AuditEventType.MessageAssigned, audit.EventType);
        Assert.Equal(MessageState.New, audit.OldState);
        Assert.Equal(MessageState.Assigned, audit.NewState);
        Assert.Equal(now, audit.Timestamp);
        Assert.Equal("assign-correlation", audit.CorrelationId);
        using var details = JsonDocument.Parse(audit.DetailsJson);
        Assert.Equal(2, details.RootElement.GetProperty("assigneeId").GetInt32());
        Assert.Equal(JsonValueKind.Null, details.RootElement.GetProperty("previousAssigneeId").ValueKind);
        await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviousApprovedReviewer_CannotBeAssignedToTheNextLevel()
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var workflow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Two levels")
            .AddStep(1, 1)
            .AddStep(2, 2);
        typeof(WorkflowDefinition).GetProperty(nameof(WorkflowDefinition.Id))!.SetValue(workflow, 1);
        var message = new Message(1, workflow.Id, 1, 1, DateTimeOffset.UtcNow);
        var reviews = new List<Review>();
        message.Assign(2);
        var review = message.StartReview(1, 2, workflow, reviews, DateTimeOffset.UtcNow);
        reviews.Add(review);
        message.Approve(review, workflow, reviews, 2, null, DateTimeOffset.UtcNow);
        message.Unassign();
        ConfigureAssignment(store, access, message, reviews, 2, Permissions.ReviewLevel2);

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1,
            new AssignMessageRequest(2), TestContext.Current.CancellationToken));

        Assert.Equal("The assignee is not eligible to review the message in its current workflow state.", exception.Message);
        store.DidNotReceive().AddAssignment(Arg.Any<Domain.Assignments.Assignment>());
        store.DidNotReceive().AddAudit(Arg.Any<AuditEvent>());
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewerWithUndoneApproval_CanBeAssignedToThatLevelAgain()
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var workflow = new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Three levels")
            .AddStep(1, 1)
            .AddStep(2, 2)
            .AddStep(3, 3);
        typeof(WorkflowDefinition).GetProperty(nameof(WorkflowDefinition.Id))!.SetValue(workflow, 1);
        var message = new Message(1, workflow.Id, 1, 1, DateTimeOffset.UtcNow);
        var reviews = new List<Review>();
        message.Assign(4);
        var first = message.StartReview(1, 4, workflow, reviews, DateTimeOffset.UtcNow);
        reviews.Add(first);
        message.Approve(first, workflow, reviews, 4, null, DateTimeOffset.UtcNow);
        message.Unassign();
        message.Assign(2);
        var second = message.StartReview(2, 2, workflow, reviews, DateTimeOffset.UtcNow);
        reviews.Add(second);
        message.Approve(second, workflow, reviews, 2, null, DateTimeOffset.UtcNow);
        message.Unassign();
        workflow.ConfigureUndo(UndoApprovalMode.LatestNonFinal, UndoActorMode.OriginalReviewer, UndoActiveReviewMode.Block);
        message.UndoLastApproval(second, workflow, reviews, 2, DateTimeOffset.UtcNow);
        ConfigureAssignment(store, access, message, reviews, 2, Permissions.ReviewLevel2);

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));

        await handler.HandleAsync(1, new AssignMessageRequest(2), TestContext.Current.CancellationToken);

        store.Received(1).AddAssignment(Arg.Any<Domain.Assignments.Assignment>());
        store.Received(1).AddAudit(Arg.Any<AuditEvent>());
        await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("CUSTOM_WORKFLOW", true, true, true, true)]
    [InlineData("DOUBLE_CONFIRMATION", true, true, true, true)]
    [InlineData("SINGLE_CONFIRMATION", true, true, true, true)]
    [InlineData("CUSTOM_WORKFLOW", false, true, true, false)]
    [InlineData("CUSTOM_WORKFLOW", true, false, true, false)]
    [InlineData("CUSTOM_WORKFLOW", true, true, false, false)]
    public async Task SelfAssignmentRequiresScopeAndPermissions(string code,
        bool canView, bool canReview, bool canAssign, bool allowed)
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var workflow = new WorkflowDefinition(code, code).AddStep(1, 1);
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate,
            true, AssignmentRetentionMode.UntilWorkflowCompleted, false);
        var message = new Message(1, 1, 1, 1, clock.UtcNow);
        ConfigureAssignment(store, access, message, [], user.UserId, Permissions.ReviewLevel1);
        store.FindWorkflowAsync(1, Arg.Any<CancellationToken>()).Returns(workflow);
        access.CheckAsync(user.UserId, 1, Arg.Any<IReadOnlyList<int>>(), Permissions.ReviewLevel1,
            Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, canView, canReview));
        access.CheckAsync(user.UserId, 1, Arg.Any<IReadOnlyList<int>>(), Permissions.MessageAssign,
            Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, canView, canAssign));
        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));

        var error = await Record.ExceptionAsync(() => handler.HandleAsync(1, new(user.UserId),
            TestContext.Current.CancellationToken));

        if (allowed)
        {
            Assert.Null(error);
            Assert.Equal(user.UserId, message.CurrentAssigneeId);
            Assert.Equal(MessageState.Assigned, message.State);
            store.Received(1).AddAssignment(Arg.Is<Domain.Assignments.Assignment>(x =>
                x.AssignedBy == user.UserId && x.AssignedTo == user.UserId));
            await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.NotNull(error);
            Assert.Null(message.CurrentAssigneeId);
            store.DidNotReceive().AddAssignment(Arg.Any<Domain.Assignments.Assignment>());
            await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidAssigneeIsRejectedWithoutChanges(int assignedTo)
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var message = new Message(1, 1, 1, 1, clock.UtcNow);
        ConfigureAssignment(store, access, message, [], 2, Permissions.ReviewLevel1);
        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1, new(assignedTo), TestContext.Current.CancellationToken));
        store.DidNotReceive().AddAssignment(Arg.Any<Domain.Assignments.Assignment>());
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignToAnotherUser_EndsPreviousAssignmentAndWritesReassignedAudit()
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var message = new Message(1, 1, 1, 1, clock.UtcNow);
        message.Assign(3);
        var previous = new Domain.Assignments.Assignment(message.Id, user.UserId, 3, clock.UtcNow.AddMinutes(-1));
        store.GetActiveAssignmentsAsync(message.Id, Arg.Any<CancellationToken>()).Returns([previous]);
        ConfigureAssignment(store, access, message, [], 2, Permissions.ReviewLevel1);
        AuditEvent? audit = null;
        store.When(x => x.AddAudit(Arg.Any<AuditEvent>())).Do(x => audit = x.Arg<AuditEvent>());
        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));

        await handler.HandleAsync(1, new(2), TestContext.Current.CancellationToken);

        Assert.Equal(2, message.CurrentAssigneeId);
        Assert.Equal(MessageState.Assigned, message.State);
        Assert.Equal(clock.UtcNow, previous.EndedAt);
        store.Received(1).AddAssignment(Arg.Is<Domain.Assignments.Assignment>(x => x.AssignedTo == 2 && x.EndedAt == null));
        Assert.NotNull(audit);
        Assert.Equal(AuditEventType.MessageReassigned, audit.EventType);
        using var details = JsonDocument.Parse(audit.DetailsJson);
        Assert.Equal(3, details.RootElement.GetProperty("previousAssigneeId").GetInt32());
        Assert.Equal(2, details.RootElement.GetProperty("assigneeId").GetInt32());
        await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task InvalidRequestedLevelCannotWrite(int level)
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var message = new Message(1, 1, 1, 1, clock.UtcNow);
        ConfigureAssignment(store, access, message, [], 2, Permissions.ReviewLevel1);
        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1, new(2, level), TestContext.Current.CancellationToken));
        store.DidNotReceive().AddAssignment(Arg.Any<Domain.Assignments.Assignment>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FutureLevelRequiresItsOwnPermission(bool permitted)
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var message = new Message(1, 1, 1, 1, clock.UtcNow);
        ConfigureAssignment(store, access, message, [], 2, Permissions.ReviewLevel1);
        access.CheckAsync(2, 1, Arg.Any<IReadOnlyList<int>>(), Permissions.ReviewLevel3, Arg.Any<CancellationToken>())
            .Returns(new UserPermissionCheck(false, true, permitted));
        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock), new InlineTransactions(),
            new MessageAuthorizationService(store, access, user, correlation, NullLogger<MessageAuthorizationService>.Instance));
        if (permitted)
        {
            await handler.HandleAsync(1, new(2, 3), TestContext.Current.CancellationToken);
            store.Received(1).AddAssignment(Arg.Is<Domain.Assignments.Assignment>(a => a.Level == 3 && a.AssignedTo == 2));
        }
        else
            await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1, new(2, 3), TestContext.Current.CancellationToken));
        Assert.Null(message.CurrentAssigneeId);
        Assert.Equal(MessageState.New, message.State);
    }

    private static (IORPStore Store, IUserAuthorizationQueries Access, ICurrentUser User, IClock Clock,
        ICorrelationContext Correlation) Dependencies()
    {
        var store = Substitute.For<IORPStore>();
        store.GetActiveAssignmentsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns([]);
        store.FindWorkflowAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new WorkflowDefinition(Guid.NewGuid().ToString("N"), "Default").AddStep(1, 1).AddStep(2, 2).AddStep(3, 3));
        var access = Substitute.For<IUserAuthorizationQueries>();
        var user = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        user.UserId.Returns(5);
        access.CheckAsync(5, 1, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })), Permissions.MessageAssign, Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, true, true));
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        correlation.CorrelationId.Returns("assignment-test");
        return (store, access, user, clock, correlation);
    }

    private static void ConfigureAssignment(IORPStore store, IUserAuthorizationQueries access, Message message,
        List<Review> reviews, int assigneeId, string reviewPermission)
    {
        store.FindMessageAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        store.FindMessageSourceAsync(message.Id, Arg.Any<CancellationToken>()).Returns(
            new MessageSourceDto(message.Id, "EXT-ASSIGN", "MT199", 1, [1], DateTimeOffset.UtcNow,
                "A", "B", MessageDirection.Incoming));
        store.GetReviewsAsync(message.Id, Arg.Any<CancellationToken>()).Returns(reviews);
        access.CheckAsync(assigneeId, 1, Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 1 })), reviewPermission, Arg.Any<CancellationToken>()).Returns(new UserPermissionCheck(false, true, true));
    }
    private sealed class InlineTransactions : ITransactionExecutor
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct);
    }

}
