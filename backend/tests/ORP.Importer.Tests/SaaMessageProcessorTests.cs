using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageProcessorTests
{
    [Theory]
    [InlineData(SaaMessageDirection.Incoming)]
    [InlineData(SaaMessageDirection.Outgoing)]
    public void AppliesDefaultDirectionOnlyWhenTheHeaderIsEmpty(SaaMessageDirection fallback)
    {
        using var file = new ImportTestFile();
        file.Write(direction: "");
        var processor = Processor(defaultDirection: fallback);
        var result = processor.Process(new SaaMessageParser().ParseFile(file.Path));
        Assert.Equal(fallback, result.Direction);
        Assert.Equal([7], result.DepartmentIds);
        Assert.Null(result.Document.Header.Direction);
        file.Write();
        Assert.Equal(SaaMessageDirection.Incoming, processor.Process(new SaaMessageParser().ParseFile(file.Path)).Direction);
    }

    [Fact]
    public void RejectsMissingDirectionInsteadOfIgnoringTheFile()
    {
        using var file = new ImportTestFile();
        file.Write(direction: "");
        Assert.Contains("Direction", Assert.Throws<SaaMessageFormatException>(() =>
            Processor().Process(new SaaMessageParser().ParseFile(file.Path))).Message);
    }

    [Fact]
    public void MatchesRulesUsingTheEffectiveDefaultDirection()
    {
        using var file = new ImportTestFile();
        file.Write(direction: "");
        var rule = ImportTestFile.Rule("MT199");
        rule.Direction = SaaMessageDirection.Outgoing;
        var processor = Processor(new SaaDepartmentRuleResolver(new[] { rule }), SaaMessageDirection.Outgoing);
        var result = processor.Process(new SaaMessageParser().ParseFile(file.Path));
        Assert.Equal(SaaRoutingStatus.Matched, result.Status);
        Assert.Equal([7], result.DepartmentIds);
        Assert.Null(result.Document.Header.Direction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void RejectsInvalidDefaultDirection(int direction) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Processor(defaultDirection: (SaaMessageDirection)direction));

    [Fact]
    public void RoutesHeaderTypeWithoutAnAllowlistOrChangingItsSpelling()
    {
        using var file = new ImportTestFile();
        file.Write(type: "mt199");
        var rules = new SaaDepartmentRuleResolver(new[] { ImportTestFile.Rule("MT199") });
        var result = Processor(rules).Process(new SaaMessageParser().ParseFile(file.Path));
        Assert.Equal(SaaRoutingStatus.Matched, result.Status);
        Assert.Equal("mt199", result.MessageType);
        Assert.Equal(3, result.BranchId);
        Assert.Equal([7], result.DepartmentIds);
    }

    [Fact]
    public void ReturnsNoDepartmentRuleAsANormalRoutingResult()
    {
        using var file = new ImportTestFile();
        file.Write(type: "MT999");
        var result = Processor(new SaaDepartmentRuleResolver(new[] { ImportTestFile.Rule("MT199") }))
            .Process(new SaaMessageParser().ParseFile(file.Path));
        Assert.Equal(SaaRoutingStatus.NoDepartmentRule, result.Status);
        Assert.Empty(result.DepartmentIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsUnknownOrInvalidBranchBeforeDepartmentRules(int? branchId)
    {
        using var file = new ImportTestFile();
        file.Write();
        var departments = new Departments();
        var processor = new SaaMessageProcessor(departments, new Branches(branchId));
        Assert.Contains("BranchId", Assert.Throws<SaaMessageFormatException>(() =>
            processor.Process(new SaaMessageParser().ParseFile(file.Path))).Message);
        Assert.Equal(0, departments.Calls);
    }

    [Fact]
    public void RoutesOverlappingRulesToBothDepartments()
    {
        using var file = new ImportTestFile();
        file.Write();
        var other = ImportTestFile.Rule("MT199");
        other.Id = 2;
        other.DepartmentId = 8;
        var processor = Processor(new SaaDepartmentRuleResolver(new[] { ImportTestFile.Rule("MT199"), other }));
        Assert.Equal([7, 8], processor.Process(new SaaMessageParser().ParseFile(file.Path)).DepartmentIds);
    }

    internal static SaaMessageProcessor Processor(ISaaMessageDepartmentResolver? departments = null,
        SaaMessageDirection? defaultDirection = null) => new(departments ?? new Departments(),
        new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 3) }), defaultDirection: defaultDirection);

    private sealed class Branches(int? id) : ISaaMessageBranchResolver
    {
        public int? ResolveBranchId(string branch) => id;
    }

    private sealed class Departments : ISaaMessageDepartmentResolver
    {
        public int Calls { get; private set; }
        public SaaDepartmentResolution? Resolve(int branchId, string messageType, SaaMessageDirection direction)
        {
            Calls++;
            return new SaaDepartmentResolution([7], new[] { 1 });
        }
    }
}
