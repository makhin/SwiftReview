using System.Text.Json;
using FluentValidation;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Assign;
using ORP.Application.Assignments.Automatic;
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
        var access = Substitute.For<IUserAccessService>();
        var user = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        var message = new Message(1, 1);
        store.FindMessageAsync(1, Arg.Any<CancellationToken>()).Returns(message);
        store.FindMessageSourceAsync(1, Arg.Any<CancellationToken>()).Returns(
            new MessageSourceDto(1, "EXT-ASSIGN", "MT199", 1, 1, DateTimeOffset.UtcNow,
                "A", "B", null, null, null, null));
        access.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(new UserAccess(2, "out-of-scope",
            new HashSet<string> { Permissions.MessageView, Permissions.ReviewLevel1 },
            new HashSet<int> { 2 }, new HashSet<int> { 2 }));
        user.UserId.Returns(5);

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1,
            new AssignMessageRequest(2), CancellationToken.None));
        store.DidNotReceive().AddAudit(Arg.Any<AuditEvent>());
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessfulAssignment_WritesCompleteAuditEvent()
    {
        var store = Substitute.For<IORPStore>();
        var access = Substitute.For<IUserAccessService>();
        var user = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        var message = new Message(1, 1);
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        AuditEvent? audit = null;
        store.FindMessageAsync(1, Arg.Any<CancellationToken>()).Returns(message);
        store.FindMessageSourceAsync(1, Arg.Any<CancellationToken>()).Returns(
            new MessageSourceDto(1, "EXT-ASSIGN", "MT199", 1, 1, now, "A", "B", null, null, null, null));
        access.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(new UserAccess(2, "assignee",
            new HashSet<string> { Permissions.MessageView, Permissions.ReviewLevel1 },
            new HashSet<int> { 1 }, new HashSet<int> { 1 }));
        user.UserId.Returns(5);
        clock.UtcNow.Returns(now);
        correlation.CorrelationId.Returns("assign-correlation");
        store.When(x => x.AddAudit(Arg.Any<AuditEvent>())).Do(x => audit = x.Arg<AuditEvent>());

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock));
        await handler.HandleAsync(1, new AssignMessageRequest(2), TestContext.Current.CancellationToken);

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
        var workflow = new WorkflowDefinition("Two levels", "MT199", 1)
            .AddStep(1, 1)
            .AddStep(2, 2);
        var message = new Message(1, workflow.Id);
        var reviews = new List<Review>();
        message.Assign(3);
        var review = message.StartReview(1, 2, workflow, reviews, DateTimeOffset.UtcNow);
        reviews.Add(review);
        message.Approve(review, workflow, reviews, null, DateTimeOffset.UtcNow);
        ConfigureAssignment(store, access, message, reviews, 2, Permissions.ReviewLevel2);

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock));

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(1,
            new AssignMessageRequest(2), TestContext.Current.CancellationToken));

        Assert.Equal("The assignee cannot review more than one level of the same message.", exception.Message);
        store.DidNotReceive().AddAssignment(Arg.Any<Domain.Assignments.Assignment>());
        store.DidNotReceive().AddAudit(Arg.Any<AuditEvent>());
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewerWithUndoneApproval_CanBeAssignedToThatLevelAgain()
    {
        var (store, access, user, clock, correlation) = Dependencies();
        var workflow = new WorkflowDefinition("Three levels", "MT199", 1)
            .AddStep(1, 1)
            .AddStep(2, 2)
            .AddStep(3, 3);
        var message = new Message(1, workflow.Id);
        var reviews = new List<Review>();
        message.Assign(3);
        var first = message.StartReview(1, 4, workflow, reviews, DateTimeOffset.UtcNow);
        reviews.Add(first);
        message.Approve(first, workflow, reviews, null, DateTimeOffset.UtcNow);
        var second = message.StartReview(2, 2, workflow, reviews, DateTimeOffset.UtcNow);
        reviews.Add(second);
        message.Approve(second, workflow, reviews, null, DateTimeOffset.UtcNow);
        message.UndoLastApproval(second, workflow, reviews, 2, DateTimeOffset.UtcNow);
        ConfigureAssignment(store, access, message, reviews, 2, Permissions.ReviewLevel2);

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, correlation,
            new AssignmentCoordinator(store, clock));

        await handler.HandleAsync(1, new AssignMessageRequest(2), TestContext.Current.CancellationToken);

        store.Received(1).AddAssignment(Arg.Any<Domain.Assignments.Assignment>());
        store.Received(1).AddAudit(Arg.Any<AuditEvent>());
        await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static (IORPStore Store, IUserAccessService Access, ICurrentUser User, IClock Clock,
        ICorrelationContext Correlation) Dependencies()
    {
        var store = Substitute.For<IORPStore>();
        var access = Substitute.For<IUserAccessService>();
        var user = Substitute.For<ICurrentUser>();
        var clock = Substitute.For<IClock>();
        var correlation = Substitute.For<ICorrelationContext>();
        user.UserId.Returns(5);
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        correlation.CorrelationId.Returns("assignment-test");
        return (store, access, user, clock, correlation);
    }

    private static void ConfigureAssignment(IORPStore store, IUserAccessService access, Message message,
        List<Review> reviews, int assigneeId, string reviewPermission)
    {
        store.FindMessageAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        store.FindMessageSourceAsync(message.Id, Arg.Any<CancellationToken>()).Returns(
            new MessageSourceDto(message.Id, "EXT-ASSIGN", "MT199", 1, 1, DateTimeOffset.UtcNow,
                "A", "B", null, null, null, null));
        store.GetReviewsAsync(message.Id, Arg.Any<CancellationToken>()).Returns(reviews);
        access.GetByIdAsync(assigneeId, Arg.Any<CancellationToken>()).Returns(new UserAccess(assigneeId,
            "assignee", new HashSet<string> { Permissions.MessageView, reviewPermission },
            new HashSet<int> { 1 }, new HashSet<int> { 1 }));
    }
}
