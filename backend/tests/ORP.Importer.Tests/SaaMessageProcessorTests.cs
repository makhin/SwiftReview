using System.Text;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageProcessorTests
{
    [Theory]
    [InlineData(SaaMessageDirection.Incoming)]
    [InlineData(SaaMessageDirection.Outgoing)]
    public void AppliesConfiguredDirectionOnlyToEmptyHeaderValues(SaaMessageDirection fallback)
    {
        WithFile(SaaMessageParserTests.Header.Replace("DIRECTION:Incoming", "DIRECTION:"), path =>
        {
            var result = new SaaMessageProcessor(new SaaMessageParser(), defaultDirection: fallback).ParseFile(path);
            Assert.Equal(fallback, result.Direction);
            Assert.Null(result.Document.Header.Direction);
        });
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var result = new SaaMessageProcessor(new SaaMessageParser(), defaultDirection: fallback).ParseFile(path);
            Assert.Equal(SaaMessageDirection.Incoming, result.Direction);
        });
    }

    [Fact]
    public void DoesNotInferDirectionWhenNoDefaultIsConfigured()
    {
        WithFile(SaaMessageParserTests.Header.Replace("DIRECTION:Incoming", "DIRECTION:"), path =>
            Assert.Null(new SaaMessageProcessor(new SaaMessageParser()).ParseFile(path).Direction));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void RejectsInvalidConfiguredDirections(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaaMessageProcessor(new SaaMessageParser(), defaultDirection: (SaaMessageDirection)value));

    [Fact]
    public void ResolvesBothRoutingIdsAtThePostParsingStage()
    {
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var branches = new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 3) });
            var departments = new Resolver(document => document.Header.MessageType.StartsWith("MX") ? 7 : 8);
            var result = new SaaMessageProcessor(new SaaMessageParser(), departments,
                branchResolver: branches, messageTypeResolver: new SaaMessageTypeResolver(new[] { "MXpain.001.001.09" })).ParseFile(path);
            Assert.Equal(3, result.BranchId);
            Assert.Equal(7, result.DepartmentId);
            Assert.Equal("MXpain.001.001.09", result.MessageType);
            Assert.Equal("PARIS", result.Document.Header.Branch);
            Assert.Equal(1, departments.Calls);
        });
    }

    [Fact]
    public void CanonicalizesTheRoutingTypeWithoutChangingTheParsedHeader()
    {
        WithFile(SaaMessageParserTests.Header.Replace("MXpain.001.001.09", "mxpain.001.001.09"), path =>
        {
            var processor = new SaaMessageProcessor(new SaaMessageParser(),
                messageTypeResolver: new SaaMessageTypeResolver(new[] { "MXpain.001.001.09" }));
            var result = processor.ParseFile(path);
            Assert.Equal("MXpain.001.001.09", result.MessageType);
            Assert.Equal("mxpain.001.001.09", result.Document.Header.MessageType);
        });
    }

    [Fact]
    public void RejectsUnsupportedTypesBeforeDepartmentRules()
    {
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var departments = new Resolver(_ => 7);
            var processor = new SaaMessageProcessor(new SaaMessageParser(), departments,
                messageTypeResolver: new SaaMessageTypeResolver(new[] { "MT199" }));
            var error = Assert.Throws<SaaMessageFormatException>(() => processor.ParseFile(path));
            Assert.Contains("MessageType", error.Message);
            Assert.Equal(path, error.FilePath);
            Assert.Equal(1, error.LineNumber);
            Assert.Equal(0, departments.Calls);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsUnmatchedOrInvalidBranchBeforeDepartmentRules(int? branchId)
    {
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var branches = new BranchResolver(_ => branchId);
            var departments = new Resolver(_ => 7);
            var processor = new SaaMessageProcessor(new SaaMessageParser(), departments,
                branchResolver: branches);
            var error = Assert.Throws<SaaMessageFormatException>(() => processor.ParseFile(path));
            Assert.Contains("BranchId", error.Message);
            Assert.Equal(path, error.FilePath);
            Assert.Equal(1, error.LineNumber);
            Assert.Equal(0, departments.Calls);
        });
    }

    [Fact]
    public void ReportsAmbiguousBranchWithFileContext()
    {
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var branches = new SaaMessageBranchResolver(new[]
            {
                new KeyValuePair<string, int>("Paris", 3),
                new KeyValuePair<string, int>("PARIS", 4)
            });
            var processor = new SaaMessageProcessor(new SaaMessageParser(), branchResolver: branches);
            var error = Assert.Throws<SaaMessageFormatException>(() => processor.ParseFile(path));
            Assert.Contains("multiple", error.Message);
            Assert.Equal(path, error.FilePath);
            Assert.Equal(1, error.LineNumber);
        });
    }

    [Fact]
    public void ResolvesDepartmentUsingValidatedHeaderAndBody()
    {
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var resolver = new Resolver(document =>
            {
                Assert.Equal("PARIS", document.Header.Branch);
                Assert.Equal("MXpain.001.001.09", document.Header.MessageType);
                Assert.Equal(new DateTime(2026, 10, 14, 4, 35, 39), document.Header.Time);
                Assert.Contains("payment content", document.Body);
                return 7;
            });
            var result = new SaaMessageProcessor(new SaaMessageParser(), resolver).ParseFile(path);
            Assert.Equal(7, result.DepartmentId);
            Assert.Equal(Path.GetFullPath(path), result.Document.FilePath);
            Assert.Equal(1, resolver.Calls);
        });
    }

    [Fact]
    public void AllowsDepartmentRulesToBeDisabled()
    {
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var result = new SaaMessageProcessor(new SaaMessageParser()).ParseFile(path);
            Assert.Null(result.BranchId);
            Assert.Null(result.DepartmentId);
            Assert.Equal(result.Document.Header.MessageType, result.MessageType);
            Assert.Equal("PARIS", result.Document.Header.Branch);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsUnresolvedOrInvalidDepartmentWhenRulesAreEnabled(int? departmentId)
    {
        WithFile(SaaMessageParserTests.Header, path =>
        {
            var processor = new SaaMessageProcessor(new SaaMessageParser(), new Resolver(_ => departmentId));
            var error = Assert.Throws<SaaMessageFormatException>(() => processor.ParseFile(path));
            Assert.Contains("DepartmentId", error.Message);
            Assert.Equal(Path.GetFullPath(path), error.FilePath);
        });
    }

    [Theory]
    [InlineData("Branch", "PARIS")]
    [InlineData("MessageType", "MXpain.001.001.09")]
    [InlineData("Time", "14/10/26 04:35:39 AM")]
    public void RejectsInvalidHeaderBeforeRunningRules(string key, string value)
    {
        WithFile(SaaMessageParserTests.Header.Replace(key + ":" + value, key + ": "), path =>
        {
            var resolver = new Resolver(_ => 7);
            var processor = new SaaMessageProcessor(new SaaMessageParser(), resolver);
            var error = Assert.Throws<SaaMessageFormatException>(() => processor.ParseFile(path));
            Assert.Contains(key, error.Message);
            Assert.Equal(1, error.LineNumber);
            Assert.Equal(Path.GetFullPath(path), error.FilePath);
            Assert.Equal(0, resolver.Calls);
        });
    }

    [Fact]
    public void DoesNotRunRulesOnInvalidFileStructure()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SaaMessageParserTests.Header);
            var resolver = new Resolver(_ => 7);
            var branchCalls = 0;
            var branches = new BranchResolver(_ => { branchCalls++; return 3; });
            var processor = new SaaMessageProcessor(new SaaMessageParser(), resolver, branchResolver: branches);
            Assert.Throws<SaaMessageFormatException>(() => processor.ParseFile(path));
            Assert.Equal(0, branchCalls);
            Assert.Equal(0, resolver.Calls);
        }
        finally { File.Delete(path); }
    }

    private sealed class BranchResolver(Func<string, int?> resolve) : ISaaMessageBranchResolver
    {
        public int? ResolveBranchId(string branch) => resolve(branch);
    }

    private sealed class Resolver(Func<SaaMessageDocument, int?> resolve) : ISaaMessageDepartmentResolver
    {
        public int Calls { get; private set; }

        public int? ResolveDepartmentId(SaaMessageDocument document)
        {
            Calls++;
            return resolve(document);
        }
    }

    private static void WithFile(string header, Action<string> action)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Join("\n", header,
                "2026/10/14-04:35:30 PRINT 1", "________", "",
                "--- Instance Type and Transmission ---", "payment content"), new UTF8Encoding(false));
            action(path);
        }
        finally { File.Delete(path); }
    }
}
