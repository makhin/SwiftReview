using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaFileImportServiceTests
{
    [Fact]
    public async Task DoesNotParseAnUnchangedIgnoredFileButRechecksChangedBytes()
    {
        using var file = new ImportTestFile();
        file.Write(type: "MT999");
        var store = new MemoryStore();
        var parser = new CountingParser();
        var service = Service(store, parser);
        Assert.Equal(SaaFileImportResult.Ignored, await service.ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(SaaFileImportResult.AlreadyIgnored, await service.ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, parser.Calls);
        var previousHash = Assert.Single(store.Ignored).Value;
        file.Write(type: "MT999", body: "changed message");
        Assert.Equal(SaaFileImportResult.Ignored, await service.ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, parser.Calls);
        Assert.NotEqual(previousHash, Assert.Single(store.Ignored).Value);
    }

    [Fact]
    public async Task RequiresExplicitRetryAfterRulesChangeAndRemovesTheMarkOnImport()
    {
        using var file = new ImportTestFile();
        file.Write(type: "MT999");
        var store = new MemoryStore();
        var parser = new CountingParser();
        Assert.Equal(SaaFileImportResult.Ignored, await Service(store, parser).ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        var service = Service(store, parser, "MT999");
        Assert.Equal(SaaFileImportResult.AlreadyIgnored, await service.ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, parser.Calls);
        Assert.Equal(SaaFileImportResult.Imported, await service.ImportAsync(file.Path, file.Root, retryIgnored: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, parser.Calls);
        Assert.Empty(store.Ignored);
        Assert.Single(store.Imported);
        Assert.Equal(SaaFileImportResult.AlreadyImported, await service.ImportAsync(file.Path, file.Root, retryIgnored: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, parser.Calls);
    }

    [Fact]
    public async Task DetectsChangedRegisteredFileBeforeParsing()
    {
        using var file = new ImportTestFile();
        file.Write();
        var parser = new CountingParser();
        var service = Service(new MemoryStore(), parser);
        Assert.Equal(SaaFileImportResult.Imported, await service.ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        file.Write(body: "different bytes");
        Assert.Equal(SaaFileImportResult.Conflict, await service.ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, parser.Calls);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("branch")]
    [InlineData("direction")]
    [InlineData("rules")]
    [InlineData("workflow")]
    public async Task ErrorsNeverCreateAnIgnoredMark(string error)
    {
        using var file = new ImportTestFile();
        file.Write(direction: error == "direction" ? "" : "Incoming", branch: error == "branch" ? "UNKNOWN" : "PARIS");
        if (error == "format") File.WriteAllText(file.Path, "broken file");
        var store = new MemoryStore { FailRegistration = error == "workflow" };
        var rules = new List<SaaDepartmentRule> { ImportTestFile.Rule("MT199") };
        if (error == "rules")
        {
            var conflicting = ImportTestFile.Rule("MT199");
            conflicting.Id = 2;
            conflicting.DepartmentId = 8;
            rules.Add(conflicting);
        }
        var service = new SaaFileImportService(new SaaSourceFileReader(), new SaaMessageParser(),
            SaaMessageProcessorTests.Processor(new SaaDepartmentRuleResolver(rules)), store);
        await Assert.ThrowsAnyAsync<FormatException>(() => service.ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(store.Ignored);
        Assert.Empty(store.Imported);
    }

    [Fact]
    public async Task FailedRetryRetainsThePreviousIgnoredMark()
    {
        using var file = new ImportTestFile();
        file.Write(type: "MT999");
        var store = new MemoryStore();
        var parser = new CountingParser();
        await Service(store, parser).ImportAsync(file.Path, file.Root, cancellationToken: TestContext.Current.CancellationToken);
        var previous = Assert.Single(store.Ignored);
        store.FailRegistration = true;
        await Assert.ThrowsAsync<FormatException>(() => Service(store, parser, "MT999")
            .ImportAsync(file.Path, file.Root, retryIgnored: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(previous, Assert.Single(store.Ignored));
    }

    [Fact]
    public void ParsesTheSameSnapshotEvenIfTheFileChangesAfterReading()
    {
        using var file = new ImportTestFile();
        file.Write(type: "MT199");
        var snapshot = new SaaSourceFileReader().Read(file.Path, file.Root);
        file.Write(type: "MT999");
        var document = new SaaMessageParser().Parse(snapshot);
        Assert.Equal("MT199", document.Header.MessageType);
        Assert.Equal(snapshot.ContentHash, document.ContentHash);
        Assert.NotEqual(snapshot.ContentHash, new SaaSourceFileReader().Read(file.Path, file.Root).ContentHash);
        Assert.Equal("sample.prt", snapshot.RelativePath);
    }

    private static SaaFileImportService Service(MemoryStore store, CountingParser parser, string type = "MT199") =>
        new(new SaaSourceFileReader(), parser,
            SaaMessageProcessorTests.Processor(new SaaDepartmentRuleResolver(new[] { ImportTestFile.Rule(type) })), store);

    private sealed class CountingParser : ISaaMessageParser
    {
        public int Calls { get; private set; }
        public SaaMessageDocument Parse(SaaSourceFileSnapshot file)
        {
            Calls++;
            return new SaaMessageParser().Parse(file);
        }
    }

    private sealed class MemoryStore : ISaaImportStore
    {
        public Dictionary<string, string> Ignored { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Imported { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool FailRegistration { get; set; }

        public Task<SaaFileImportResult?> FindAsync(string path, string hash, bool retryIgnored, CancellationToken cancellationToken)
        {
            if (Imported.TryGetValue(path, out var imported))
                return Task.FromResult<SaaFileImportResult?>(imported == hash ? SaaFileImportResult.AlreadyImported : SaaFileImportResult.Conflict);
            return Task.FromResult<SaaFileImportResult?>(!retryIgnored && Ignored.TryGetValue(path, out var ignored) && ignored == hash
                ? SaaFileImportResult.AlreadyIgnored : null);
        }

        public Task<SaaFileImportResult> IgnoreAsync(SaaSourceFileSnapshot file, CancellationToken cancellationToken)
        {
            Ignored[file.RelativePath] = file.ContentHash;
            return Task.FromResult(SaaFileImportResult.Ignored);
        }

        public Task<SaaFileImportResult> RegisterAsync(SaaSourceFileSnapshot file, SaaProcessedMessage message, CancellationToken cancellationToken)
        {
            if (FailRegistration) throw new FormatException("No active workflow matches.");
            Imported[file.RelativePath] = file.ContentHash;
            Ignored.Remove(file.RelativePath);
            return Task.FromResult(SaaFileImportResult.Imported);
        }
    }
}
