using ORP.Domain.Messages;
using Xunit;

namespace ORP.Domain.Tests;

public sealed class MessageRegistrationTests
{
    [Fact]
    public void RegistrationSetsAllRoutingFieldsAndStartsUnassigned()
    {
        var receivedAt = new DateTimeOffset(2026, 10, 15, 12, 30, 0, TimeSpan.Zero);
        var message = new Message(101, 4, 2, 3, receivedAt);
        Assert.Equal(101, message.Id);
        Assert.Equal(4, message.WorkflowDefinitionId);
        Assert.Equal(2, message.BranchId);
        Assert.Equal([3], message.DepartmentIds);
        Assert.Equal(receivedAt, message.ReceivedAt);
        Assert.Equal(MessageState.New, message.State);
        Assert.Null(message.CurrentAssigneeId);
    }

    [Fact]
    public void RegistrationStoresEachDepartmentOnceInStableOrder()
    {
        var message = new Message(101, 4, 2, [8, 3, 8], DateTimeOffset.UtcNow);
        Assert.Equal([3, 8], message.DepartmentIds);
    }

    [Theory]
    [InlineData(0, 4, 2, 3)]
    [InlineData(101, 0, 2, 3)]
    [InlineData(101, 4, 0, 3)]
    [InlineData(101, 4, 2, 0)]
    [InlineData(101, -1, 2, 3)]
    [InlineData(101, 4, -1, 3)]
    [InlineData(101, 4, 2, -1)]
    public void RegistrationRejectsInvalidIds(long id, int workflowId, int branchId, int departmentId) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            new Message(id, workflowId, branchId, departmentId, DateTimeOffset.UtcNow));
}
