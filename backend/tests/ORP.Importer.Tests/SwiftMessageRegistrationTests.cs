using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SwiftMessageRegistrationTests
{
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
            var record = SqlSaaImportStore.CreateRecord(result.Document, Path.GetTempPath(), result.Direction);
            Assert.Equal(expected, record.Direction);
            Assert.Null(result.Document.Header.Direction);
        }, direction: false, defaultDirection: fallback);
    }

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
