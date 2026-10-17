using ORP.Domain.Workflows;
using Xunit;
namespace ORP.Domain.Tests;
public sealed class WorkflowCodeTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RequiresNonEmptyCode(string code) =>
        Assert.Throws<ArgumentException>(() => new WorkflowDefinition(code, "Test"));

    [Fact]
    public void DefinitionContainsPolicyIndependentOfRouting()
    {
        var workflow = new WorkflowDefinition("SINGLE", "Single confirmation").AddStep(1, 1);
        Assert.Equal("SINGLE", workflow.Code);
        Assert.Equal(new[] { 1 }, workflow.RequiredLevels());
    }
}
