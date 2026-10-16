using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SwiftMessageRegistrationTests
{
    [Fact]
    public void SelectsSeriesWorkflowAndPrefersAnExactOverride()
    {
        WithProcessed(result =>
        {
            var series = Workflow(type: "MT1xx");
            Assert.Same(series, SqlSaaImportStore.SelectWorkflow([series], result));
            var exact = Workflow();
            Assert.Same(exact, SqlSaaImportStore.SelectWorkflow([series, exact], result));
            Assert.Contains("Multiple", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow([series, Workflow(type: "MT1xx")], result)).Message);
        });
    }

    [Fact]
    public void PrefersBranchSpecificWorkflowOverTheGeneralWorkflow()
    {
        WithProcessed(result =>
        {
            var general = Workflow();
            var scoped = Workflow(branch: 3);
            Assert.Same(scoped, SqlSaaImportStore.SelectWorkflow(new[] { general, scoped }, result));
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
            Assert.Same(valid, SqlSaaImportStore.SelectWorkflow(candidates, result));
        });
    }

    [Fact]
    public void RejectsAmbiguousWorkflowsInsteadOfChoosingTheFirst()
    {
        WithProcessed(result =>
        {
            Assert.Contains("Multiple", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { Workflow(), Workflow() }, result)).Message);
            Assert.Contains("Multiple", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { Workflow(), Workflow(branch: 3), Workflow(branch: 3) }, result)).Message);
        });
    }

    [Fact]
    public void RejectsMissingAndInvalidWorkflowSteps()
    {
        WithProcessed(result =>
        {
            Assert.Contains("No active", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(Array.Empty<WorkflowDefinition>(), result)).Message);
            var invalid = new WorkflowDefinition(MessageDirection.Incoming, "Invalid", "MT199", 7);
            Assert.Contains("review steps", Assert.Throws<FormatException>(() =>
                SqlSaaImportStore.SelectWorkflow(new[] { invalid }, result)).Message);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfiguredThirdReviewIsSupported(bool thirdRequired)
    {
        WithProcessed(result =>
        {
            var workflow = Workflow().AddStep(2, 2).AddStep(3, 3, thirdRequired);
            Assert.Same(workflow, SqlSaaImportStore.SelectWorkflow(new[] { workflow }, result));
        });
    }

    [Fact]
    public void SelectsConfiguredScopedWorkflowRegardlessOfDepartmentName()
    {
        WithProcessed(result =>
        {
            var general = Workflow();
            var scoped = Workflow(branch: 3).AddStep(2, 2).AddStep(3, 3, required: false);
            Assert.Same(scoped, SqlSaaImportStore.SelectWorkflow(new[] { general, scoped }, result));
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
            Assert.Same(workflow, SqlSaaImportStore.SelectWorkflow(new[] { workflow }, result));
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
            new([7], new[] { 1 });
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
