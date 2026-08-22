using SwiftReview.Domain.Assignments;
using SwiftReview.Domain.Common;
using SwiftReview.Domain.Outbox;
using Xunit;

namespace SwiftReview.Domain.Tests;

public sealed class OutboxAndAssignmentTests
{
    [Fact]
    public void OutboxLease_PreventsConcurrentClaimAndProcessedReplay()
    {
        var now = new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        var item = new OutboxMessage("MessageChanged", "{}", now, "corr");
        var owner = Guid.NewGuid();
        Assert.True(item.TryLock(now, TimeSpan.FromMinutes(1), owner));
        Assert.False(item.TryLock(now.AddSeconds(30), TimeSpan.FromMinutes(1), Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => item.MarkProcessed(now.AddSeconds(40), Guid.NewGuid()));
        item.MarkProcessed(now.AddSeconds(40), owner);
        Assert.False(item.TryLock(now.AddMinutes(2), TimeSpan.FromMinutes(1), Guid.NewGuid()));
        Assert.Equal(1, item.Attempts);
    }

    [Fact]
    public void AssignmentHistory_CanEndOnlyOnce()
    {
        var now = DateTimeOffset.UtcNow; var assignment = new Assignment(1, 1, 2, now);
        assignment.End(now.AddMinutes(1));
        Assert.NotNull(assignment.EndedAt);
        Assert.Throws<DomainRuleViolationException>(() => assignment.End(now.AddMinutes(2)));
    }

    [Fact]
    public void OutboxFailure_RequiresOwnerAndDefersNextAttempt()
    {
        var now = new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        var item = new OutboxMessage("MessageChanged", "{}", now, "corr");
        var owner = Guid.NewGuid();
        item.TryLock(now, TimeSpan.FromMinutes(1), owner);

        Assert.Throws<InvalidOperationException>(() => item.MarkFailed("failure", now.AddMinutes(1), Guid.NewGuid()));
        item.MarkFailed("failure", now.AddMinutes(1), owner);
        Assert.False(item.TryLock(now.AddSeconds(30), TimeSpan.FromMinutes(1), Guid.NewGuid()));
        Assert.True(item.TryLock(now.AddMinutes(1), TimeSpan.FromMinutes(1), Guid.NewGuid()));
    }
}
