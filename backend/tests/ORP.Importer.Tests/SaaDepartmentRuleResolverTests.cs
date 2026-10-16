using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaDepartmentRuleResolverTests
{
    [Theory]
    [InlineData(1, "MXpacs.008", SaaMessageDirection.Incoming, 1)]
    [InlineData(1, "MXpacs.008.001.08", SaaMessageDirection.Incoming, 1)]
    [InlineData(2, "MT999", SaaMessageDirection.Incoming, 1)]
    [InlineData(1, "MT799", SaaMessageDirection.Outgoing, 2)]
    [InlineData(3, "MT094", SaaMessageDirection.Incoming, 3)]
    [InlineData(4, "MT400", SaaMessageDirection.Incoming, 2)]
    [InlineData(5, "MT400", SaaMessageDirection.Incoming, 2)]
    public void IncludesEveryMatchingDepartmentByBranch(int branchId, string type, SaaMessageDirection direction, int expected)
    {
        var resolver = new SaaDepartmentRuleResolver(new[]
        {
            Rule(1, 1, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MxFamily, "MXpacs.008"),
            Rule(2, 2, 1, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MxFamily, "MXpacs.008"),
            Rule(3, 1, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT999"),
            Rule(4, 2, 2, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT999"),
            Rule(5, 2, 1, SaaMessageDirection.Outgoing, SaaDepartmentMatchKind.MtSeries, "MT7xx"),
            Rule(6, 3, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT094"),
            Rule(7, 2, 4, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MtSeries, "MT4xx"),
            Rule(8, 2, 5, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MtSeries, "MT4xx")
        });
        Assert.Contains(expected, resolver.Resolve(branchId, type, direction)!.DepartmentIds);
    }

    [Theory]
    [InlineData("MT399", false)]
    [InlineData("MT400", true)]
    [InlineData("MT499", true)]
    [InlineData("MT500", false)]
    [InlineData("MT4xx", false)]
    public void MtSeriesHasExactBounds(string type, bool matches)
    {
        var resolver = new SaaDepartmentRuleResolver(new[] { Rule(1, 2, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MtSeries, "MT4xx") });
        Assert.Equal(matches, resolver.Resolve(1, type, SaaMessageDirection.Incoming) is not null);
    }

    [Fact]
    public void MxFamilyMatchesVersionsButVersionedRuleIsExact()
    {
        var resolver = new SaaDepartmentRuleResolver(new[]
        {
            Rule(1, 1, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MxFamily, "MXpacs.008"),
            Rule(2, 3, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MxFamily, "MXcamt.029.001.09")
        });
        Assert.Equal(1, resolver.Resolve(1, "MXpacs.008.001.08", SaaMessageDirection.Incoming)!.DepartmentIds.Single());
        Assert.Null(resolver.Resolve(1, "MXpacs.0080", SaaMessageDirection.Incoming));
        Assert.Equal(3, resolver.Resolve(1, "MXcamt.029.001.09", SaaMessageDirection.Incoming)!.DepartmentIds.Single());
        Assert.Null(resolver.Resolve(1, "MXcamt.029.001.10", SaaMessageDirection.Incoming));
        Assert.Null(resolver.Resolve(1, "MXcamt.029.001.09.extra", SaaMessageDirection.Incoming));
        Assert.Null(resolver.Resolve(1, "pacs.008.001.08", SaaMessageDirection.Incoming));
    }

    [Fact]
    public void MatchesTheExactMxTypeUsedBySaaHeaders()
    {
        var resolver = new SaaDepartmentRuleResolver(new[]
        {
            Rule(1, 1, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MXcamt.055.001.08")
        });
        var header = new SaaMessageParser().ParseLine(SaaMessageParserTests.Header
            .Replace("MXpain.001.001.09", "MXcamt.055.001.08"));
        Assert.Equal(1, resolver.Resolve(1, header.MessageType, header.Direction!.Value)!.DepartmentIds.Single());
        Assert.Equal(1, resolver.Resolve(1, "mxcamt.055.001.08", SaaMessageDirection.Incoming)!.DepartmentIds.Single());
        Assert.Null(resolver.Resolve(1, "camt.055.001.08", SaaMessageDirection.Incoming));
        Assert.Null(resolver.Resolve(1, "MXcamt.055.001.09", SaaMessageDirection.Incoming));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("camt.")]
    [InlineData("camt.055.*")]
    [InlineData("camt.055")]
    [InlineData("MXcamt.")]
    [InlineData("MXcamt.055.*")]
    [InlineData("MXcamt.055.001")]
    [InlineData("MXcamt.055.001.0x")]
    [InlineData("MXcamt.055.001.08.extra")]
    [InlineData(" MXcamt.055 ")]
    public void RejectsMalformedMxPatternsAtStartup(string pattern)
    {
        foreach (var kind in new[] { SaaDepartmentMatchKind.Exact, SaaDepartmentMatchKind.MxFamily })
            Assert.Throws<InvalidOperationException>(() => new SaaDepartmentRuleResolver(new[]
            {
                Rule(1, 1, null, SaaMessageDirection.Incoming, kind, pattern)
            }));
    }

    [Theory]
    [InlineData("MXpacs.008")]
    [InlineData("MXcamt.029.001.09")]
    [InlineData("mxadmi.024")]
    public void AcceptsCanonicalMxPatterns(string pattern)
    {
        foreach (var kind in new[] { SaaDepartmentMatchKind.Exact, SaaDepartmentMatchKind.MxFamily })
        {
            var resolver = new SaaDepartmentRuleResolver(new[]
            {
                Rule(1, 1, null, SaaMessageDirection.Incoming, kind, pattern)
            });
            Assert.Equal(1, resolver.Resolve(1, pattern, SaaMessageDirection.Incoming)!.DepartmentIds.Single());
        }
    }

    [Fact]
    public void IgnoresDisabledRulesAndReportsAllWinningRuleIds()
    {
        var resolver = new SaaDepartmentRuleResolver(new[]
        {
            Rule(1, 1, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT199"),
            Rule(2, 1, 1, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT199"),
            Rule(3, 3, 1, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT199", false)
        });
        var result = resolver.Resolve(1, "MT199", SaaMessageDirection.Incoming)!;
        Assert.Equal([1], result.DepartmentIds);
        Assert.Equal(new[] { 2 }, result.RuleIds);
        Assert.Null(resolver.Resolve(1, "MT299", SaaMessageDirection.Incoming));
    }

    [Fact]
    public void ReturnsMultipleDepartmentsForOverlappingRules()
    {
        var resolver = new SaaDepartmentRuleResolver(new[]
        {
            Rule(1, 1, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT199"),
            Rule(2, 2, 1, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.Exact, "MT199")
        });
        Assert.Equal([1, 2], resolver.Resolve(1, "MT199", SaaMessageDirection.Incoming)!.DepartmentIds);
    }

    [Fact]
    public void RejectsEmptyOrInvalidActiveRules()
    {
        Assert.Throws<InvalidOperationException>(() => new SaaDepartmentRuleResolver(Array.Empty<SaaDepartmentRule>()));
        Assert.Throws<InvalidOperationException>(() => new SaaDepartmentRuleResolver(new[]
        {
            Rule(1, 1, null, SaaMessageDirection.Incoming, SaaDepartmentMatchKind.MtSeries, "MT4*")
        }));
    }

    private static SaaDepartmentRule Rule(int id, int departmentId, int? branchId,
        SaaMessageDirection direction, SaaDepartmentMatchKind kind, string pattern, bool active = true) =>
        new() { Id = id, DepartmentId = departmentId, BranchId = branchId, Direction = direction,
            MatchKind = kind, MessageTypePattern = pattern, IsActive = active };
}
