namespace ORP.Importer;

public enum SaaFileImportResult { Imported, AlreadyImported, Ignored, AlreadyIgnored, Conflict }

public interface ISaaImportStore
{
    Task<SaaFileImportResult?> FindAsync(string relativePath, string contentHash, bool retryIgnored,
        CancellationToken cancellationToken = default);
    Task<SaaFileImportResult> IgnoreAsync(SaaSourceFileSnapshot file, CancellationToken cancellationToken = default);
    Task<SaaFileImportResult> RegisterAsync(SaaSourceFileSnapshot file, SaaProcessedMessage message,
        CancellationToken cancellationToken = default);
}

/// <summary>Coordinates one file; persistence and routing policy belong to its collaborators.</summary>
public sealed class SaaFileImportService(SaaSourceFileReader reader, ISaaMessageParser parser,
    SaaMessageProcessor processor, ISaaImportStore store)
{
    public async Task<SaaFileImportResult> ImportAsync(string path, string rootDirectory, bool retryIgnored = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = reader.Read(path, rootDirectory);
        var existing = await store.FindAsync(file.RelativePath, file.ContentHash, retryIgnored, cancellationToken);
        if (existing.HasValue) return existing.Value;
        var message = processor.Process(parser.Parse(file));
        return message.Status == SaaRoutingStatus.NoDepartmentRule
            ? await store.IgnoreAsync(file, cancellationToken)
            : await store.RegisterAsync(file, message, cancellationToken);
    }
}
