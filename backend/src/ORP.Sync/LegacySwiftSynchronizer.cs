using Microsoft.Extensions.Logging;

namespace ORP.Sync;

internal static class LegacySwiftSynchronizer
{
    public static void Run(string connectionString, ILoggerFactory loggerFactory, string correlationId)
    {
        var logger = loggerFactory.CreateLogger("ORP.Sync.LegacySwiftSynchronizer");
        var repository = new SwiftMessageRepository(connectionString,
            loggerFactory.CreateLogger<SwiftMessageRepository>());
        var routing = RoutingRules.FromConfiguration();
        repository.ValidateRoutingReferences(routing);
        var toUtc = DateTime.UtcNow;
        var fromUtc = repository.GetFromUtc(toUtc);
        SyncLog.SwiftQueryStarted(logger, fromUtc, toUtc);
        var messages = new SwiftQueryClient(loggerFactory.CreateLogger<SwiftQueryClient>())
            .GetMessages(fromUtc, toUtc);
        SyncLog.SwiftQueryCompleted(logger, messages.Count);
        var result = repository.Save(messages, routing, toUtc, correlationId);
        SyncLog.SyncCompleted(logger, messages.Count, result.Inserted, result.Skipped,
            result.RoutingIssues, toUtc, correlationId);
    }
}
