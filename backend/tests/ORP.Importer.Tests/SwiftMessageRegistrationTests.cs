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
            Assert.Same(scoped, SwiftMessageImporter.SelectWorkflow(new[] { general, scoped }, result, "DC"));
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
            Assert.Same(valid, SwiftMessageImporter.SelectWorkflow(candidates, result, "DC"));
        });
    }

    [Fact]
    public void RejectsAmbiguousWorkflowsInsteadOfChoosingTheFirst()
    {
        WithProcessed(result =>
        {
            Assert.Contains("Multiple", Assert.Throws<FormatException>(() =>
                SwiftMessageImporter.SelectWorkflow(new[] { Workflow(), Workflow() }, result, "DC")).Message);
            Assert.Contains("Multiple", Assert.Throws<FormatException>(() =>
                SwiftMessageImporter.SelectWorkflow(new[] { Workflow(), Workflow(branch: 3), Workflow(branch: 3) }, result, "DC")).Message);
        });
    }

    [Fact]
    public void RejectsMissingAndInvalidWorkflowSteps()
    {
        WithProcessed(result =>
        {
            Assert.Contains("No active", Assert.Throws<FormatException>(() =>
                SwiftMessageImporter.SelectWorkflow(Array.Empty<WorkflowDefinition>(), result, "DC")).Message);
            var invalid = new WorkflowDefinition(MessageDirection.Incoming, "Invalid", "MT199", 7);
            Assert.Contains("review steps", Assert.Throws<FormatException>(() =>
                SwiftMessageImporter.SelectWorkflow(new[] { invalid }, result, "DC")).Message);
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
                SwiftMessageImporter.SelectWorkflow(new[] { workflow }, result, departmentName));
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
            Assert.Same(workflow, SwiftMessageImporter.SelectWorkflow(new[] { workflow }, result, departmentName));
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
                SwiftMessageImporter.SelectWorkflow(new[] { general, scoped }, result, "CS")).Message);
        });
    }

    [Theory]
    [InlineData(false, true, true, true, "MessageType")]
    [InlineData(true, false, true, true, "BranchId")]
    [InlineData(true, true, false, true, "DepartmentId")]
    [InlineData(true, true, true, false, "direction")]
    public void RejectsIncompleteRoutingBeforeOpeningTheDatabase(bool validateType, bool branch, bool department,
        bool direction, string expected)
    {
        WithProcessed(result =>
        {
            var importer = new SwiftMessageImporter("Server=localhost,1;Database=unused;User Id=unused;Password=unused");
            Assert.Contains(expected, Assert.Throws<FormatException>(() =>
            {
                _ = importer.SaveAsync(result, Path.GetTempPath(), TestContext.Current.CancellationToken);
            }).Message);
        }, validateType, branch, department, direction);
    }

    [Fact]
    public void AcceptsValidatedRoutingAndTracksThatTheAllowlistWasApplied()
    {
        WithProcessed(result =>
        {
            Assert.True(result.IsMessageTypeValidated);
            SwiftMessageImporter.ValidateRouting(result);
        });
    }

    [Theory]
    [InlineData(SaaMessageDirection.Incoming, MessageDirection.Incoming)]
    [InlineData(SaaMessageDirection.Outgoing, MessageDirection.Outgoing)]
    public void RegistersUsingConfiguredDirectionWhilePreservingTheSourceDocument(SaaMessageDirection fallback,
        MessageDirection expected)
    {
        WithProcessed(result =>
        {
            SwiftMessageImporter.ValidateRouting(result);
            var workflow = Workflow(direction: expected);
            Assert.Same(workflow, SwiftMessageImporter.SelectWorkflow(new[] { workflow }, result, "DC"));
            var record = SwiftMessageImporter.CreateRecord(result.Document, Path.GetTempPath(), result.Direction);
            Assert.Equal(expected, record.Direction);
            Assert.Null(result.Document.Header.Direction);
        }, direction: false, defaultDirection: fallback);
    }

    private static WorkflowDefinition Workflow(string type = "MT199", int department = 7, int? branch = null,
        MessageDirection direction = MessageDirection.Incoming) =>
        new WorkflowDefinition(direction, "Workflow", type, department, branch).AddStep(1, 1);

    private sealed class Departments : ISaaMessageDepartmentResolver
    {
        public int? ResolveDepartmentId(SaaMessageDocument document) => 7;
    }

    private static void WithProcessed(Action<SaaProcessedMessage> action, bool validateType = true, bool branch = true,
        bool department = true, bool direction = true, SaaMessageDirection? defaultDirection = null)
    {
        var path = Path.GetTempFileName();
        try
        {
            var header = SaaMessageParserTests.Header.Replace("MXpain.001.001.09", "MT199");
            if (!direction) header = header.Replace("DIRECTION:Incoming", "DIRECTION:");
            File.WriteAllText(path, string.Join("\n", header, "2026/10/15-04:35:30 PRINT 1", "________", "",
                "--- Instance Type and Transmission ---", "content"));
            var processor = new SaaMessageProcessor(new SaaMessageParser(), department ? new Departments() : null,
                branchResolver: branch ? new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 3) }) : null,
                messageTypeResolver: validateType ? new SaaMessageTypeResolver(new[] { "MT199" }) : null,
                defaultDirection: defaultDirection);
            action(processor.ParseFile(path));
        }
        finally { File.Delete(path); }
    }
}
