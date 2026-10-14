using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Messages;
using ORP.Importer;

namespace ORP.Infrastructure.Persistence;

public enum SwiftMessageImportResult
{
    Imported,
    AlreadyImported,
    Conflict
}

public sealed class SwiftMessageImporter(string connectionString)
{
    private readonly DbContextOptions<ORPDbContext> options = new DbContextOptionsBuilder<ORPDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp"))
        .Options;

    public async Task<SwiftMessageImportResult> SaveAsync(SaaMessageDocument document, string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        var record = CreateRecord(document, rootDirectory);
        var path = record.FilePath;

        await using var db = new ORPDbContext(options);
        var existingHash = await db.SwiftMessages.AsNoTracking()
            .Where(message => message.FilePath == path)
            .Select(message => message.ContentHash)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingHash is not null)
            return CompareHash(existingHash, document.ContentHash);

        db.SwiftMessages.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return SwiftMessageImportResult.Imported;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A second importer may have won the unique FilePath index after our lookup.
            await using var retry = new ORPDbContext(options);
            var winnerHash = await retry.SwiftMessages.AsNoTracking()
                .Where(message => message.FilePath == path)
                .Select(message => message.ContentHash)
                .SingleOrDefaultAsync(cancellationToken);
            if (winnerHash is null)
                throw;
            return CompareHash(winnerHash, document.ContentHash);
        }
    }

    internal static SwiftMessageRecord CreateRecord(SaaMessageDocument document, string rootDirectory)
    {
        var path = Path.GetRelativePath(Path.GetFullPath(rootDirectory), document.FilePath)
            .Replace('\\', '/');
        if (path == ".." || path.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(path))
            throw new ArgumentException("The file must be inside the SAA messages directory.", nameof(document));

        EnsureLength(path, 450, "FilePath");
        EnsureLength(document.FileName, 255, "FileName");
        EnsureLength(document.Header.Branch, 100, "Branch");
        EnsureLength(document.Header.MessageType, 100, "MessageType");
        EnsureLength(document.Header.ReferenceId, 255, "ReferenceId");
        EnsureLength(document.Header.Sender, 100, "Sender");
        EnsureLength(document.Header.Receiver, 100, "Receiver");
        EnsureLength(document.Header.Host, 255, "Host");

        return new SwiftMessageRecord
        {
            Branch = document.Header.Branch,
            MessageType = document.Header.MessageType,
            ReferenceId = document.Header.ReferenceId,
            Direction = document.Header.Direction switch
            {
                SaaMessageDirection.Incoming => MessageDirection.Incoming,
                SaaMessageDirection.Outgoing => MessageDirection.Outgoing,
                _ => null
            },
            Sender = document.Header.Sender,
            Receiver = document.Header.Receiver,
            Host = document.Header.Host,
            Time = document.Header.Time,
            Body = document.Body,
            FileName = document.FileName,
            FilePath = path,
            ContentHash = document.ContentHash
        };
    }

    private static SwiftMessageImportResult CompareHash(string saved, string current) =>
        string.Equals(saved, current, StringComparison.OrdinalIgnoreCase)
            ? SwiftMessageImportResult.AlreadyImported
            : SwiftMessageImportResult.Conflict;

    private static void EnsureLength(string? value, int maximum, string field)
    {
        if (value?.Length > maximum)
            throw new FormatException($"{field} is longer than {maximum} characters.");
    }
}
