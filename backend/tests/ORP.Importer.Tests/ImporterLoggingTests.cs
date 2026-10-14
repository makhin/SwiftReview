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
        });
    }

    [Fact]
    public void UnresolvedDepartmentIsLogged()
    {
        WithFile("private payment content", path =>
        {
            var logger = new CaptureLogger<SaaMessageProcessor>();
            var processor = new SaaMessageProcessor(new SaaMessageParser(), new UnresolvedDepartment(), logger);
            Assert.Throws<SaaMessageFormatException>(() => processor.ParseFile(path));
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(3003, entry.Event.Id);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal(path, entry.Properties["FilePath"]);
        });
    }

    private sealed class UnresolvedDepartment : ISaaMessageDepartmentResolver
    {
        public int? ResolveDepartmentId(SaaMessageDocument document) => null;
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
