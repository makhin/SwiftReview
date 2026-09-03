using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Common;
using Xunit;

namespace SwiftReview.Domain.Tests;

public sealed class AssignmentHistoryTests
{
    [Fact]
    public void AssignmentHistory_CanEndOnlyOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var assignment = new Assignment(1, 1, 2, now);

        assignment.End(now.AddMinutes(1));

        Assert.NotNull(assignment.EndedAt);
        Assert.Throws<DomainRuleViolationException>(() => assignment.End(now.AddMinutes(2)));
    }
}
