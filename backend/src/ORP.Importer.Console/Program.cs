using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ORP.Importer.ConsoleApp;
using Microsoft.Extensions.Logging;
using ORP.Importer;
using ORP.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory
});
using var host = builder.Build();
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("ORP.Importer.Console");

if (args.Length != 1)
{
    ConsoleImportLog.ConfigurationInvalid(logger, "Usage: ORP.Importer.Console <SAAMessages directory>");
    return 2;
}

var directory = Path.GetFullPath(args[0]);
if (!Directory.Exists(directory))
{
    ConsoleImportLog.ConfigurationInvalid(logger, $"Directory does not exist: {directory}");
    return 2;
}

var processor = new SaaMessageProcessor(new SaaMessageParser(loggerFactory.CreateLogger<SaaMessageParser>()),
    logger: loggerFactory.CreateLogger<SaaMessageProcessor>());
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ORP");
if (string.IsNullOrWhiteSpace(connectionString))
{
    ConsoleImportLog.ConfigurationInvalid(logger, "ConnectionStrings__ORP is required to import SAA messages.");
    return 2;
}
var importer = new SwiftMessageImporter(connectionString, loggerFactory.CreateLogger<SwiftMessageImporter>());
var processed = 0;
var skipped = 0;
var failed = 0;

ConsoleImportLog.ScanStarted(logger, directory);

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
                failed++;
                continue;
            }
            if (result == SwiftMessageImportResult.Imported)
                processed++;
            else
                skipped++;
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            // Parser validation failures have already been logged with their line number.
            if (exception is not SaaMessageFormatException)
                ConsoleImportLog.FileProcessingFailed(logger, exception, path);
            failed++;
        }
        catch (Exception exception) when (exception is SqlException or DbUpdateException)
        {
            ConsoleImportLog.ImportStopped(logger, exception, path);
            return 2;
        }
    }
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    ConsoleImportLog.DirectoryScanFailed(logger, exception, directory);
    return 2;
}

ConsoleImportLog.ScanCompleted(logger, processed + skipped + failed, processed, skipped, failed);
return failed == 0 ? 0 : 1;
