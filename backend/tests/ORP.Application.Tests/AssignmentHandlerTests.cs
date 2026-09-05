using System.Text.Json;
using FluentValidation;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Assign;
using ORP.Domain.Auditing;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
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

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, clock, correlation);
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

        var handler = new AssignMessageHandler(store, access, new AssignMessageValidator(), user, clock, correlation);
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
}
