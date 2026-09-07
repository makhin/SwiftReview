namespace ORP.Sync;

internal static class LegacySwiftSynchronizer
{
    public static void Run(string connectionString)
    {
        var repository = new SwiftMessageRepository(connectionString);
        var routing = RoutingRules.FromConfiguration();
        repository.ValidateRoutingReferences(routing);
        var toUtc = DateTime.UtcNow;
        var fromUtc = repository.GetFromUtc(toUtc);
        Console.WriteLine($"Querying Swift messages from {fromUtc:O} to {toUtc:O}.");
        var messages = new SwiftQueryClient().GetMessages(fromUtc, toUtc);
        var correlationId = $"sync-{Guid.NewGuid():N}";
        var result = repository.Save(messages, routing, toUtc, correlationId);
        Console.WriteLine($"Swift synchronization completed. Read={messages.Count}, Inserted={result.Inserted}, Skipped={result.Skipped}, RoutingIssues={result.RoutingIssues}, CorrelationId={correlationId}.");
    }
}
