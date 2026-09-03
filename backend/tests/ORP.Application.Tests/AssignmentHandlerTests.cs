using FluentValidation;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Assign;
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
        await store.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
