using ORP.Importer;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class WorkflowRoutingMatchTests
{
    [Theory]
    [InlineData("MT4xx", "MT400", true)]
    [InlineData("MT4xx", "MT450", true)]
    [InlineData("mt4XX", "mt499", true)]
    [InlineData("MT4xx", "MT399", false)]
    [InlineData("MT4xx", "MT500", false)]
    [InlineData("MT4xx", "MT4000", false)]
    [InlineData("MT4xx", "MT4ab", false)]
    [InlineData("MT4xx", "MT4\u0660\u0661", false)]
    [InlineData("MT4*", "MT450", false)]
    [InlineData("MT4**", "MT450", false)]
    [InlineData("MT450", "mt450", true)]
    [InlineData("MT450", "MT451", false)]
    [InlineData("MXpacs.008", "MXpacs.008", true)]
    [InlineData("MXpacs.008", "mxPACS.008.001.08", true)]
    [InlineData("MXpacs.008", "MXpacs.008.002.09", true)]
    [InlineData("MXpacs.008", "MXpacs.0080", false)]
    [InlineData("MXpacs.008", "MXpacs.009.001.08", false)]
    [InlineData("MXpacs.008", "MXpacs.008.invalid", false)]
    [InlineData("MXpacs.008", "MXpacs.008.001.08.extra", false)]
    [InlineData("MXpacs.008", "pacs.008.001.08", false)]
    [InlineData("MXpacs.008.001.08", "mxpacs.008.001.08", true)]
    [InlineData("MXpacs.008.001.08", "MXpacs.008.001.09", false)]
    [InlineData("MXpacs.008.001.08", "MXpacs.008", false)]
    public void MatchesExactTypesMtSeriesAndMxFamilies(string pattern, string type, bool matches)
    {
        var workflow = Workflow(pattern);
        var selected = Select([workflow], type);
        if (matches) Assert.Same(workflow, Assert.Single(selected));
        else Assert.Empty(selected);
    }

    [Theory]
    [InlineData("MT4xx", "MT450")]
    [InlineData("MXpacs.008", "MXpacs.008.001.08")]
    public void ExactTypeWinsWithinTheSameBranchScope(string pattern, string type)
    {
        var exact = Workflow(type);
        Assert.Same(exact, Assert.Single(Select([Workflow(pattern), exact], type)));
        Assert.Same(exact, Assert.Single(Select([exact, Workflow(pattern)], type)));
    }

    [Fact]
    public void BranchScopeTakesPrecedenceOverPatternSpecificity()
    {
        var scoped = Workflow("MT4xx", branch: 3);
        Assert.Same(scoped, Assert.Single(Select([Workflow("MT450"), scoped], "MT450")));
    }

    [Fact]
    public void ExactTypeAlsoWinsWithinBranchScope()
    {
        var exact = Workflow("MT450", branch: 3);
        Assert.Same(exact, Assert.Single(Select([Workflow("MT4xx", branch: 3), exact], "MT450")));
    }

    [Fact]
    public void IgnoresInactiveAndOtherScopeCandidates()
    {
        var valid = Workflow("MT4xx");
        var inactive = Workflow("MT450", branch: 3);
        inactive.IsActive = false;
        Assert.Same(valid, Assert.Single(Select([
            Workflow("MT4xx", branch: 4),
            Workflow("MT4xx", direction: SaaMessageDirection.Outgoing), inactive, valid], "MT450")));
    }

    [Theory]
    [InlineData("MT4xx", "MT450")]
    [InlineData("MXpacs.008", "MXpacs.008.001.08")]
    [InlineData("MT450", "MT450")]
    public void ReturnsAllEquallyPreferredCandidatesSoCallersCanRejectAmbiguity(string pattern, string type)
    {
        Assert.Equal(2, Select([Workflow(pattern), Workflow(pattern)], type).Length);
    }

    [Fact]
    public void ResolvesDepartmentsIndependently()
    {
        var rules = new[] { Workflow("MT450"), Workflow("MT4xx", department: 8) };
        Assert.Equal(new[] { 7, 8 }, Select(rules, "MT450").Select(x => x.DepartmentId).Order());
    }

    [Fact]
    public void ConflictingPreferredPoliciesAreRejected()
    {
        var first = Workflow("MT450");
        var second = Workflow("MT450");
        first.WorkflowDefinitionId = 1;
        second.WorkflowDefinitionId = 2;
        Assert.Throws<FormatException>(() => Select([first, second], "MT450"));
    }

    private static SaaDepartmentRule[] Select(SaaDepartmentRule[] rules, string type) =>
        SaaDepartmentRuleResolver.SelectRules(rules, 3, type, SaaMessageDirection.Incoming);

    private static SaaDepartmentRule Workflow(string pattern, int? branch = null, int department = 7,
        SaaMessageDirection direction = SaaMessageDirection.Incoming) => new()
        {
            Id = 1, DepartmentId = department, BranchId = branch, Direction = direction, IsActive = true,
            WorkflowDefinitionId = 1, MessageTypePattern = pattern,
            MatchKind = pattern.EndsWith("xx", StringComparison.OrdinalIgnoreCase) ? SaaDepartmentMatchKind.MtSeries :
                pattern.StartsWith("MX", StringComparison.OrdinalIgnoreCase) && pattern.Count(c => c == '.') == 1
                    ? SaaDepartmentMatchKind.MxFamily : SaaDepartmentMatchKind.Exact
        };
}
