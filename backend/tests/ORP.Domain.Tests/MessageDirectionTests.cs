using ORP.Domain.Common;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using Xunit;

namespace ORP.Domain.Tests;

public sealed class MessageDirectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void WorkflowRejectsUnknownDirection(int value) =>
        Assert.Throws<DomainRuleViolationException>(() => new WorkflowDefinition((MessageDirection)value, "Test", "MT299", 1));

    [Theory]
    [InlineData(MessageDirection.Incoming)]
    [InlineData(MessageDirection.Outgoing)]
    public void WorkflowHasExplicitDirection(MessageDirection direction)
    {
        var workflow = new WorkflowDefinition(direction, "Test", "MT299", 1).AddStep(1, 1);
        Assert.Equal(direction, workflow.Direction);
    }
}
