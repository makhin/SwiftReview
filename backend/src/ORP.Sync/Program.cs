using System;
using System.Configuration;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ORP.Sync;

internal static class Program
{
    public static int Main()
    {
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(ReadLogLevel());
            logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffK ";
            });
        });
        var logger = loggerFactory.CreateLogger("ORP.Sync");
        var correlationId = $"sync-{Guid.NewGuid():N}";
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
        try
        {
            SyncLog.SyncStarted(logger, correlationId);
            var connectionString = ConfigurationManager.ConnectionStrings["ORP"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ConfigurationErrorsException("Connection string 'ORP' is required.");

            LegacySwiftSynchronizer.Run(connectionString!, loggerFactory, correlationId);
            return 0;
        }
        catch (Exception exception)
        {
            SyncLog.SyncFailed(logger, exception, correlationId);
            return 1;
        }
    }

    private static LogLevel ReadLogLevel() =>
        Enum.TryParse(ConfigurationManager.AppSettings["LogLevel"], true, out LogLevel level)
            ? level
            : LogLevel.Information;
}
