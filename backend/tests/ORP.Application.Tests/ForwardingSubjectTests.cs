using Microsoft.Extensions.Logging.Abstractions;
using ORP.Application.Emails;
using ORP.Domain.Emails;
using Xunit;

namespace ORP.Application.Tests;

public sealed class ForwardingSubjectTests
{
    private readonly ForwardingSubjectResolver resolver = new(NullLogger<ForwardingSubjectResolver>.Instance);
    private static ForwardingSubjectContext Context => new("MT299", 1, [2, 3], "ref20", "ref21");
    private static ForwardingSubjectRule Rule(int id = 1, int priority = 100, string template = "FW: {MessageType} - {Field21}") =>
        new() { Id = id, Name = "TFO", IsActive = true, Priority = priority, DepartmentId = 2, Template = template };

    [Fact]
    public void TfoUsesRelatedReferenceWithoutRequiringField20()
    {
        Assert.Equal("FW: MT299 - ref21", resolver.Resolve(Context with { Field20 = null }, [Rule()]));
        Assert.Equal("ref20/ref21/MT299", resolver.Resolve(Context, [Rule(template: "{Field20}/{Field21}/{MessageType}")]));
        Assert.Equal("FW: MT299 - {Field20}", resolver.Resolve(Context with { Field21 = "{Field20}" }, [Rule()]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingReferenceFallsBack(string? value) =>
        Assert.Equal("FW: MT299", resolver.Resolve(Context with { Field21 = value }, [Rule()]));

    [Fact]
    public void AllFiltersMustMatchAndTypeIgnoresCase()
    {
        var rule = Rule();
        rule.BranchId = 1;
        rule.MessageType = "mt299";
        Assert.Equal("FW: MT299 - ref21", resolver.Resolve(Context, [rule]));
        Assert.Equal("FW: MT299", resolver.Resolve(Context with { BranchId = 9 }, [rule]));
        Assert.Equal("FW: MT199", resolver.Resolve(Context with { MessageType = "MT199" }, [rule]));
        Assert.Equal("FW: MT299", resolver.Resolve(Context with { ProcessingDepartmentIds = [3] }, [rule]));
        rule.DepartmentId = null;
        Assert.Equal("FW: MT299 - ref21", resolver.Resolve(Context with { ProcessingDepartmentIds = [3] }, [rule]));
    }

    [Fact]
    public void PriorityThenIdAndInactiveRulesAreRespected()
    {
        var first = Rule(2, 200, "first");
        var second = Rule(3, 200, "second");
        var lower = Rule(1, 100, "lower");
        Assert.Equal("first", resolver.Resolve(Context, [lower, second, first]));
        first.IsActive = false;
        Assert.Equal("second", resolver.Resolve(Context, [first, lower, second]));
    }

    [Theory]
    [InlineData("{Unknown}")]
    [InlineData("{MessageType")]
    [InlineData("MessageType}")]
    [InlineData("{{Field21}}")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("bad\r\nsubject")]
    public void InvalidRuleFallsThroughToNextRule(string template) =>
        Assert.Equal("next", resolver.Resolve(Context, [Rule(template: template), Rule(2, 0, "next")]));

    [Fact]
    public void MissingTokenFallsThroughAndRenderedResultMustBeValid()
    {
        Assert.Equal("next", resolver.Resolve(Context with { Field20 = null },
            [Rule(template: "{Field20}"), Rule(2, 0, "next")]));
        Assert.Equal("FW: MT299", resolver.Resolve(Context with { Field21 = "bad\nreference" }, [Rule()]));
        Assert.Equal("FW: MT299", resolver.Resolve(Context with { Field21 = new string('a', 500) }, [Rule()]));
        Assert.Equal(new string('a', 500), resolver.Resolve(Context, [Rule(template: new string('a', 500))]));
        Assert.Equal("FW: MT299", resolver.Resolve(Context, [Rule(template: new string('a', 501))]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad\ntype")]
    public void UnusableTypeUsesSafeDefault(string? type) =>
        Assert.Equal("FW: SWIFT message", resolver.Resolve(Context with { MessageType = type }, []));
}
