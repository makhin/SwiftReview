using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Authorization;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using Xunit;

namespace ORP.Application.Tests;

public sealed class UndoAuthorizationTests
{
    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(false, false, false, true, true)]
    public async Task UndoRequiresScopedViewActionAndWorkflowAccess_UnlessDatabaseIdentityIsAdministrator(
        bool view, bool permission, bool workflow, bool administrator, bool allowed)
    {
        var ct = TestContext.Current.CancellationToken;
        var store = Substitute.For<IORPStore>();
        var users = Substitute.For<IUserAuthorizationQueries>();
        var actor = Substitute.For<ICurrentUser>();
        var correlation = Substitute.For<ICorrelationContext>();
        actor.UserId.Returns(7);
        // A stale transport claim must neither grant nor remove administrative access.
        actor.IsGlobalAdministrator.Returns(!administrator);
        store.FindMessageAsync(1, ct).Returns(new Message(1, 9));
        store.FindMessageSourceAsync(1, ct).Returns(new MessageSourceDto(1, "MSG", "MT199", 10, 20, DateTimeOffset.UtcNow, "", "", MessageDirection.Incoming));
        store.GetReviewsAsync(1, ct).Returns([]);
        users.CheckAsync(7, 10, 20, Permissions.ReviewUndo, ct).Returns(new UserPermissionCheck(administrator, view, permission));
        users.CanAccessWorkflowAsync(7, 9, ct).Returns(workflow);
        var service = new MessageAuthorizationService(store, users, actor, correlation, NullLogger<MessageAuthorizationService>.Instance);
        if (allowed)
        {
            var result = await service.RequireAsync(1, Permissions.ReviewUndo, ct);
            Assert.Equal(administrator, result.IsGlobalAdministrator);
        }
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RequireAsync(1, Permissions.ReviewUndo, ct));
        await users.Received(1).CheckAsync(7, 10, 20, Permissions.ReviewUndo, ct);
    }
}
