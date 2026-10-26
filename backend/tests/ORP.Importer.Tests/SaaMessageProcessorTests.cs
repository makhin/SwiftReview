using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageProcessorTests
{
    [Fact]
    public void RejectsMissingDirectionInsteadOfIgnoringTheFile()
    {
        using var file = new ImportTestFile();
        file.Write(direction: "");
        Assert.Contains("'DIRECTION' must not be empty", Assert.Throws<SaaMessageFormatException>(() =>
            Processor().Process(new SaaMessageParser().ParseFile(file.Path))).Message);
    }

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

    internal static SaaMessageProcessor Processor(ISaaMessageDepartmentResolver? departments = null) => new(departments ?? new Departments(),
        new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 3) }));

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
