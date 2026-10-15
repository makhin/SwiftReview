using Microsoft.Extensions.Logging;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class ImporterLoggingTests
{
    [Fact]
    public void ParsingLogsFileAndHashWithoutMessageContents()
    {
        WithFile("private payment content", path =>
        {
            var logger = new CaptureLogger<SaaMessageParser>();
            var document = new SaaMessageParser(logger).ParseFile(path);
            Assert.Equal(new[] { 3000, 3001 }, logger.Entries.Select(x => x.Event.Id));
            var completed = logger.Entries[1];
            Assert.Equal(LogLevel.Debug, completed.Level);
            Assert.Equal(path, completed.Properties["FilePath"]);
            Assert.Equal(document.ContentHash, completed.Properties["ContentHash"]);
            Assert.DoesNotContain("private payment content", string.Join(" ", completed.Properties.Values));
            Assert.DoesNotContain(document.Header.ReferenceId!, string.Join(" ", completed.Properties.Values));
        });
    }

    [Fact]
    public void ParsingFailureLogsTheOriginalExceptionAndLineNumber()
    {
        WithFile("private payment content", path =>
        {
            File.WriteAllText(path, "invalid");
            var logger = new CaptureLogger<SaaMessageParser>();
            var error = Assert.Throws<SaaMessageFormatException>(() => new SaaMessageParser(logger).ParseFile(path));
            var entry = logger.Entries.Single(x => x.Event.Id == 3002);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Same(error, entry.Exception);
            Assert.Equal(path, entry.Properties["FilePath"]);
            Assert.Equal(1, entry.Properties["LineNumber"]);
            Assert.Equal(error.Reason, entry.Properties["Reason"]);
        });
    }

    [Fact]
    public void RoutingLogsDepartmentAndWinningRulesWithoutDumpingTheHeader()
    {
        WithFile("private payment content", path =>
        {
            var logger = new CaptureLogger<SaaMessageProcessor>();
            var departments = new SaaDepartmentRuleResolver(new[]
            {
                new SaaDepartmentRule { Id = 12, DepartmentId = 7, Direction = SaaMessageDirection.Incoming,
                    MatchKind = SaaDepartmentMatchKind.Exact, MessageTypePattern = "MXpain.001.001.09", IsActive = true }
            });
            var branches = new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 3) });
            var document = new SaaMessageParser().ParseFile(path);
            var processor = new SaaMessageProcessor(departments, branches, logger);
            Assert.Equal(7, processor.Process(document).DepartmentId);
            var resolved = logger.Entries.Single(entry => entry.Event.Id == 3009);
            Assert.Equal(7, resolved.Properties["DepartmentId"]);
            Assert.Equal("12", resolved.Properties["RuleIds"]);
            Assert.Equal(3, resolved.Properties["BranchId"]);
            Assert.Equal("MXpain.001.001.09", resolved.Properties["MessageType"]);
            Assert.Equal(SaaMessageDirection.Incoming, resolved.Properties["Direction"]);
            Assert.DoesNotContain("private payment content", string.Join(" ", resolved.Properties.Values));
            var emptyBranches = new SaaMessageBranchResolver(Array.Empty<KeyValuePair<string, int>>());
            Assert.Throws<SaaMessageFormatException>(() =>
                new SaaMessageProcessor(departments, emptyBranches, logger).Process(document));
            Assert.Equal(3005, logger.Entries.Last().Event.Id);
            Assert.Equal("PARIS", logger.Entries.Last().Properties["Branch"]);
            Assert.Contains("BranchId", (string)logger.Entries.Last().Properties["Reason"]!);
        });
    }

    [Fact]
    public void NoRuleLogsRoutingContextAsInformationWithoutMessageContents()
    {
        WithFile("private payment content", path =>
        {
            var logger = new CaptureLogger<SaaMessageProcessor>();
            var rules = new SaaDepartmentRuleResolver(new[] { ImportTestFile.Rule("MT199") });
            var branches = new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 3) });
            var document = new SaaMessageParser().ParseFile(path);
            var result = new SaaMessageProcessor(rules, branches, logger).Process(document);
            Assert.Equal(SaaRoutingStatus.NoDepartmentRule, result.Status);
            var entry = logger.Entries.Single(x => x.Event.Id == 3010);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(path, entry.Properties["FilePath"]);
            Assert.Equal(3, entry.Properties["BranchId"]);
            Assert.Equal(document.Header.MessageType, entry.Properties["MessageType"]);
            Assert.Equal(SaaMessageDirection.Incoming, entry.Properties["Direction"]);
            Assert.DoesNotContain("private payment content", string.Join(" ", entry.Properties.Values));
            Assert.DoesNotContain(document.Header.ReferenceId!, string.Join(" ", entry.Properties.Values));
            Assert.DoesNotContain(logger.Entries, x => x.Level >= LogLevel.Warning);
        });
    }

    private sealed record Entry(LogLevel Level, EventId Event, Exception? Exception,
        Dictionary<string, object?> Properties);

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<Entry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(x => x.Key, x => x.Value);
            Entries.Add(new Entry(logLevel, eventId, exception, properties));
        }
    }

    private static void WithFile(string body, Action<string> action)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, string.Join("\n", SaaMessageParserTests.Header,
                "2026/10/14-04:35:30 PRINT 1", "________", "",
                "--- Instance Type and Transmission ---", body));
            action(path);
        }
        finally { File.Delete(path); }
    }
}
