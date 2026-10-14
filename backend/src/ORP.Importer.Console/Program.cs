using ORP.Importer;
using ORP.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ORP.Importer.Console <SAAMessages directory>");
    return 2;
}

var directory = Path.GetFullPath(args[0]);
if (!Directory.Exists(directory))
{
    Console.Error.WriteLine($"Directory does not exist: {directory}");
    return 2;
}

var processor = new SaaMessageProcessor(new SaaMessageParser());
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ORP");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__ORP is required to import SAA messages.");
    return 2;
}
var importer = new SwiftMessageImporter(connectionString);
var processed = 0;
var skipped = 0;
var failed = 0;

try
{
    var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
        .Where(path => string.Equals(Path.GetExtension(path), ".prt", StringComparison.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    foreach (var path in files)
    {
        try
        {
            var processedMessage = processor.ParseFile(path);
            var document = processedMessage.Document;
            var result = await importer.SaveAsync(document, directory);
            if (result == SwiftMessageImportResult.Conflict)
            {
                Console.Error.WriteLine($"Conflict: {path}: the saved file has different content.");
                failed++;
                continue;
            }
            var header = document.Header;
            Console.WriteLine($"ImportResult: {result}");
            Console.WriteLine($"FileName: {document.FileName}");
            Console.WriteLine($"FilePath: {document.FilePath}");
            Console.WriteLine($"ContentHash: {document.ContentHash}");
            Console.WriteLine($"Branch: {Display(header.Branch)}");
            Console.WriteLine($"MessageType: {Display(header.MessageType)}");
            Console.WriteLine($"ReferenceId: {Display(header.ReferenceId)}");
            Console.WriteLine($"Direction: {Display(header.Direction?.ToString())}");
            Console.WriteLine($"Sender: {Display(header.Sender)}");
            Console.WriteLine($"Receiver: {Display(header.Receiver)}");
            Console.WriteLine($"Host: {Display(header.Host)}");
            Console.WriteLine($"Time: {header.Time:O}");
            Console.WriteLine("Body:");
            Console.Write(document.Body);
            if (!document.Body.EndsWith('\n') && !document.Body.EndsWith('\r'))
                Console.WriteLine();
            Console.WriteLine();
            if (result == SwiftMessageImportResult.Imported)
                processed++;
            else
                skipped++;
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: {path}: {exception.Message}");
            failed++;
        }
        catch (Exception exception) when (exception is SqlException or DbUpdateException)
        {
            Console.Error.WriteLine($"Database error: {exception.Message}");
            return 2;
        }
    }
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Cannot scan directory {directory}: {exception.Message}");
    return 2;
}

Console.WriteLine($"Scanned: {processed + skipped + failed}; imported: {processed}; already imported: {skipped}; errors: {failed}.");
return failed == 0 ? 0 : 1;

static string Display(string? value) => value ?? "<empty>";
