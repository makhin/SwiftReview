using NSubstitute;
using SwiftReview.Application.Abstractions;
using SwiftReview.Application.ReferenceData;
using SwiftReview.Domain.Identity;
using Xunit;

namespace SwiftReview.Application.Tests;

public sealed class ReferenceDataHandlerTests
{
    [Fact]
    public async Task Users_AreAvailableToMessageViewersWithoutAssignPermission()
    {
        var ct = TestContext.Current.CancellationToken;
        var (queries, users, current) = Dependencies(new HashSet<string> { Permissions.MessageView });
        queries.GetUsersAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UserSummaryDto>());

        await new GetUsersHandler(queries, users, current).HandleAsync(ct);

        await queries.Received(1).GetUsersAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lookups_RejectUsersWithoutMessageViewPermission()
    {
        var ct = TestContext.Current.CancellationToken;
        var (queries, users, current) = Dependencies(new HashSet<string>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetBranchesHandler(queries, users, current).HandleAsync(ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetDepartmentsHandler(queries, users, current).HandleAsync(ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetMessageTypesHandler(queries, users, current).HandleAsync(ct));

        await queries.DidNotReceive().GetBranchesAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>());
        await queries.DidNotReceive().GetDepartmentsAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>());
        await queries.DidNotReceive().GetMessageTypesAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>());
    }

    private static (IReferenceDataQueries Queries, IUserAccessService Users, ICurrentUser Current) Dependencies(
        IReadOnlySet<string> permissions)
    {
        var queries = Substitute.For<IReferenceDataQueries>();
        var users = Substitute.For<IUserAccessService>();
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(1);
        users.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new UserAccess(1, "viewer", permissions,
            new HashSet<int> { 1 }, new HashSet<int> { 1 }));
        return (queries, users, current);
    }
}
