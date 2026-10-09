using NSubstitute;
using ORP.Application.Abstractions;
using ORP.Application.Dashboard.GetSummary;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using Xunit;

namespace ORP.Application.Tests;

public sealed class DashboardSummaryTests
{
    [Fact]
    public void GroupsBusinessStagesWithoutLosingOtherMessagesFromTotal()
    {
        var counts = new Dictionary<MessageState, int>
        {
            [MessageState.New] = 12,
            [MessageState.Assigned] = 5,
            [MessageState.FirstReviewInProgress] = 2,
            [MessageState.WaitingForSecondReview] = 4,
            [MessageState.SecondReviewInProgress] = 1,
            [MessageState.WaitingForThirdReview] = 3,
            [MessageState.ThirdReviewInProgress] = 2,
            [MessageState.Completed] = 20,
            [MessageState.Rejected] = 1
        };

        var summary = DashboardSummaryDto.FromStateCounts(counts);

        Assert.Equal(new DashboardSummaryDto(50, 12, 7, 5, 20), summary);
        Assert.Equal(new DashboardSummaryDto(0, 0, 0, 0, 0),
            DashboardSummaryDto.FromStateCounts(new Dictionary<MessageState, int>()));
    }

    [Fact]
    public async Task UsesCurrentUsersAccessForSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        var queries = Substitute.For<IMessageQueries>();
        var users = Substitute.For<IUserAccessService>();
        var current = Substitute.For<ICurrentUser>();
        var access = new UserAccess(1, "reviewer", "Reviewer", false,
            [new UserScopeAccess(10, 20, [], [Permissions.MessageView])]);
        current.UserId.Returns(1);
        users.GetByIdAsync(1, ct).Returns(access);
        var expected = new DashboardSummaryDto(3, 1, 1, 1, 0);
        queries.DashboardAsync(access, ct).Returns(expected);

        var actual = await new GetDashboardSummaryHandler(queries, users, current).HandleAsync(ct);

        Assert.Equal(expected, actual);
        await queries.Received(1).DashboardAsync(access, ct);
    }
}
