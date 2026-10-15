using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ORP.Importer.ConsoleApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ORP.Importer;
using ORP.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddJsonFile("appsettings.Importer.local.json", optional: true)
    .AddEnvironmentVariables();
using var host = builder.Build();
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("ORP.Importer.Console");

if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--retry-ignored"))
{
    ConsoleImportLog.ConfigurationInvalid(logger, "Usage: ORP.Importer.Console <SAAMessages directory> [--retry-ignored]");
    return 2;
}

var directory = Path.GetFullPath(args[0]);
if (!Directory.Exists(directory))
{
    ConsoleImportLog.ConfigurationInvalid(logger, $"Directory does not exist: {directory}");
    return 2;
}

var connectionString = builder.Configuration.GetConnectionString("ORP");
if (string.IsNullOrWhiteSpace(connectionString))
{
    ConsoleImportLog.ConfigurationInvalid(logger, "ConnectionStrings:ORP is required to import SAA messages.");
    return 2;
}
var store = new SqlSaaImportStore(connectionString, loggerFactory.CreateLogger<SqlSaaImportStore>());
var catalog = new SaaRoutingCatalog(connectionString);
var retryIgnored = args.Length == 2;
var processed = 0;
var skipped = 0;
var failed = 0;
var ignored = 0;
var alreadyIgnored = 0;

ConsoleImportLog.ScanStarted(logger, directory, retryIgnored);

try
{
    var defaultDirection = builder.Configuration.GetValue<SaaMessageDirection?>("Importer:DefaultDirection");
    var branches = await catalog.LoadBranchResolverAsync();
    var departments = await catalog.LoadDepartmentResolverAsync();
    var processor = new SaaMessageProcessor(departments, branches,
        loggerFactory.CreateLogger<SaaMessageProcessor>(), defaultDirection);
    var importer = new SaaFileImportService(new SaaSourceFileReader(),
        new SaaMessageParser(loggerFactory.CreateLogger<SaaMessageParser>()), processor, store);
    var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
        .Where(path => string.Equals(Path.GetExtension(path), ".prt", StringComparison.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();

    foreach (var path in files)
    {
        try
        {
            var result = await importer.ImportAsync(path, directory, retryIgnored);
            switch (result)
            {
                case SaaFileImportResult.Imported: processed++; break;
                case SaaFileImportResult.AlreadyImported: skipped++; break;
                case SaaFileImportResult.Ignored: ignored++; break;
                case SaaFileImportResult.AlreadyIgnored: alreadyIgnored++; break;
                case SaaFileImportResult.Conflict: failed++; break;
            }
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            // Parser validation failures have already been logged with their line number.
            if (exception is not SaaMessageFormatException)
                ConsoleImportLog.FileProcessingFailed(logger, exception, path, exception.Message);
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
catch (Exception exception) when (exception is SqlException or DbUpdateException or InvalidOperationException or ArgumentException)
{
    ConsoleImportLog.ImportStopped(logger, exception, directory);
    return 2;
}

ConsoleImportLog.ScanCompleted(logger, processed + skipped + ignored + alreadyIgnored + failed,
    processed, skipped, ignored, alreadyIgnored, failed);
return failed == 0 ? 0 : 1;
