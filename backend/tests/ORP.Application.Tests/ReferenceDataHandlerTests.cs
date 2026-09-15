using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.ReferenceData;
using ORP.Domain.Identity;
using Xunit;

namespace ORP.Application.Tests;

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
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new GetMessageStatesHandler(users, current).HandleAsync(ct));

        await queries.DidNotReceive().GetBranchesAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>());
        await queries.DidNotReceive().GetDepartmentsAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>());
        await queries.DidNotReceive().GetMessageTypesAsync(Arg.Any<UserAccess>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageStates_ReturnStableCodesAndLabels()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, users, current) = Dependencies(new HashSet<string> { Permissions.MessageView });

        var states = await new GetMessageStatesHandler(users, current).HandleAsync(ct);

        Assert.Equal(9, states.Count);
        Assert.Equal("New", states[0].Label);
        Assert.Contains(states, state => state.Code == "WaitingForSecondReview" && state.Label == "Waiting for second review");
        Assert.Equal("Rejected", states[^1].Label);
        Assert.Equal(Enum.GetNames<ORP.Domain.Messages.MessageState>(), states.Select(state => state.Code));
    }

    [Theory]
    [InlineData("New", 1, MessageStagePhase.Waiting, "Waiting for first review assignment", null)]
    [InlineData("Assigned", 1, MessageStagePhase.Assigned, "Waiting for first review assignment", "Assigned for first review")]
    [InlineData("FirstReviewInProgress", 1, MessageStagePhase.Reviewing, "First review in progress", null)]
    [InlineData("WaitingForSecondReview", 2, MessageStagePhase.Waiting, "Waiting for second review assignment", "Assigned for second review")]
    [InlineData("SecondReviewInProgress", 2, MessageStagePhase.Reviewing, "Second review in progress", null)]
    [InlineData("WaitingForThirdReview", 3, MessageStagePhase.Waiting, "Waiting for third review assignment", "Assigned for third review")]
    [InlineData("ThirdReviewInProgress", 3, MessageStagePhase.Reviewing, "Third review in progress", null)]
    [InlineData("Completed", null, MessageStagePhase.Completed, "All required reviews completed", null)]
    [InlineData("Rejected", null, MessageStagePhase.Rejected, "Review rejected", null)]
    public async Task MessageStates_DescribeEveryStage(string code, int? level, MessageStagePhase phase,
        string description, string? assignedDescription)
    {
        var (_, users, current) = Dependencies(new HashSet<string> { Permissions.MessageView });
        var states = await new GetMessageStatesHandler(users, current).HandleAsync(TestContext.Current.CancellationToken);
        var state = Assert.Single(states, state => state.Code == code);
        Assert.Equal(level, state.ReviewLevel);
        Assert.Equal(phase, state.Phase);
        Assert.Equal(description, state.Description);
        Assert.Equal(assignedDescription, state.AssignedDescription);
    }

    private static (IReferenceDataQueries Queries, IUserAccessService Users, ICurrentUser Current) Dependencies(
        IReadOnlySet<string> permissions)
    {
        var queries = Substitute.For<IReferenceDataQueries>();
        var users = Substitute.For<IUserAccessService>();
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(1);
        users.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new UserAccess(1, "viewer", "Viewer", false, [new UserScopeAccess(1, 1, [], permissions.ToArray())]));
        return (queries, users, current);
    }
}
