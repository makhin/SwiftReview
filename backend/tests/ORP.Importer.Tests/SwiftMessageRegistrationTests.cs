using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SwiftMessageRegistrationTests
{
    [Fact]
    public void PrefersBranchSpecificWorkflowOverTheGeneralWorkflow()
    {
        WithProcessed(result =>
        {
            var general = Workflow();
            var scoped = Workflow(branch: 3);
            Assert.Same(scoped, SqlSaaImportStore.SelectWorkflow(new[] { general, scoped }, result, "DC"));
        });
    }

    [Fact]
    public void IgnoresInactiveAndOtherTypeDirectionBranchOrDepartmentWorkflows()
    {
        WithProcessed(result =>
        {
            var valid = Workflow(type: "mt199");
            var inactive = Workflow();
            inactive.Deactivate();
            var candidates = new[] { Workflow(type: "MT299"), Workflow(direction: MessageDirection.Outgoing),
                Workflow(branch: 4), Workflow(department: 8), inactive, valid };
            Assert.Same(valid, SqlSaaImportStore.SelectWorkflow(candidates, result, "DC"));
        });
    }

    [Fact]
    public void RejectsAmbiguousWorkflowsInsteadOfChoosingTheFirst()
    {
        WithProcessed(result =>
        {
            Assert.Contains("Multiple", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { Workflow(), Workflow() }, result, "DC")).Message);
            Assert.Contains("Multiple", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { Workflow(), Workflow(branch: 3), Workflow(branch: 3) }, result, "DC")).Message);
        });
    }

    [Fact]
    public void RejectsMissingAndInvalidWorkflowSteps()
    {
        WithProcessed(result =>
        {
            Assert.Contains("No active", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(Array.Empty<WorkflowDefinition>(), result, "DC")).Message);
            var invalid = new WorkflowDefinition(MessageDirection.Incoming, "Invalid", "MT199", 7);
            Assert.Contains("review steps", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { invalid }, result, "DC")).Message);
        });
    }

    [Theory]
    [InlineData("CS", true)]
    [InlineData("CS", false)]
    [InlineData("cs", false)]
    [InlineData("TFO", true)]
    [InlineData("TFO", false)]
    [InlineData("tFo", false)]
    public void RejectsThirdReviewLevelForCsAndTfoIncludingOptionalSteps(string departmentName, bool thirdRequired)
    {
        WithProcessed(result =>
        {
            var workflow = Workflow().AddStep(2, 2).AddStep(3, 3, thirdRequired);
            var error = Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { workflow }, result, departmentName));
            Assert.Contains("CS and TFO", error.Message);
        });
    }

    [Theory]
    [InlineData("CS", 1)]
    [InlineData("CS", 2)]
    [InlineData("cs", 2)]
    [InlineData("TFO", 1)]
    [InlineData("TFO", 2)]
    [InlineData("tfo", 2)]
    [InlineData("DC", 3)]
    public void AcceptsReviewLevelsAllowedForTheDepartment(string departmentName, int levels)
    {
        WithProcessed(result =>
        {
            var workflow = Workflow();
            for (var level = 2; level <= levels; level++) workflow.AddStep(level, level);
            Assert.Same(workflow, SqlSaaImportStore.SelectWorkflow(new[] { workflow }, result, departmentName));
        });
    }

    [Fact]
    public void RejectsInvalidScopedCsWorkflowInsteadOfFallingBackToGeneralWorkflow()
    {
        WithProcessed(result =>
        {
            var general = Workflow();
            var scoped = Workflow(branch: 3).AddStep(2, 2).AddStep(3, 3, required: false);
            Assert.Contains("CS and TFO", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { general, scoped }, result, "CS")).Message);
        });
    }

    [Fact]
    public void RejectsNoDepartmentRuleBeforeOpeningTheDatabase()
    {
        WithProcessed(result => Assert.Throws<FormatException>(() => SqlSaaImportStore.ValidateRouting(result)),
            department: false);
    }

    [Fact]
    public void AcceptsRoutingWithoutAnAllowlist()
    {
        WithProcessed(SqlSaaImportStore.ValidateRouting);
    }

    [Theory]
    [InlineData(SaaMessageDirection.Incoming, MessageDirection.Incoming)]
    [InlineData(SaaMessageDirection.Outgoing, MessageDirection.Outgoing)]
    public void RegistersUsingConfiguredDirectionWhilePreservingTheSourceDocument(SaaMessageDirection fallback,
        MessageDirection expected)
    {
        WithProcessed(result =>
        {
            SqlSaaImportStore.ValidateRouting(result);
            var workflow = Workflow(direction: expected);
            Assert.Same(workflow, SqlSaaImportStore.SelectWorkflow(new[] { workflow }, result, "DC"));
            var record = SqlSaaImportStore.CreateRecord(result.Document, Path.GetTempPath(), result.Direction);
            Assert.Equal(expected, record.Direction);
            Assert.Null(result.Document.Header.Direction);
        }, direction: false, defaultDirection: fallback);
    }

    private static WorkflowDefinition Workflow(string type = "MT199", int department = 7, int? branch = null,
        MessageDirection direction = MessageDirection.Incoming) =>
        new WorkflowDefinition(direction, "Workflow", type, department, branch).AddStep(1, 1);

    private sealed class Departments : ISaaMessageDepartmentResolver
    {
        public SaaDepartmentResolution? Resolve(int branchId, string messageType, SaaMessageDirection direction) =>
            new(7, new[] { 1 });
    }

    private static void WithProcessed(Action<SaaProcessedMessage> action, bool department = true,
        bool direction = true, SaaMessageDirection? defaultDirection = null)
    {
        var path = Path.GetTempFileName();
        try
        {
            var header = SaaMessageParserTests.Header.Replace("MXpain.001.001.09", "MT199");
            if (!direction) header = header.Replace("DIRECTION:Incoming", "DIRECTION:");
            File.WriteAllText(path, string.Join("\n", header, "2026/10/15-04:35:30 PRINT 1", "________", "",
                "--- Instance Type and Transmission ---", "content"));
            ISaaMessageDepartmentResolver resolver = department ? new Departments() : new SaaDepartmentRuleResolver(new[]
            {
                new SaaDepartmentRule { Id = 1, DepartmentId = 7, Direction = SaaMessageDirection.Incoming,
                    MatchKind = SaaDepartmentMatchKind.Exact, MessageTypePattern = "MT299", IsActive = true }
            });
            var processor = new SaaMessageProcessor(resolver,
                new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 3) }),
                defaultDirection: defaultDirection);
            action(processor.Process(new SaaMessageParser().ParseFile(path)));
        }
        finally { File.Delete(path); }
    }
}
