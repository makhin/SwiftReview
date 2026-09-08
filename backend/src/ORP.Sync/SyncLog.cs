using System;
using Microsoft.Extensions.Logging;

namespace ORP.Sync;

internal static partial class SyncLog
{
    [LoggerMessage(EventId = 3000, EventName = "SyncStarted", Level = LogLevel.Information,
        Message = "Swift synchronization started; correlation {CorrelationId}")]
    public static partial void SyncStarted(ILogger logger, string correlationId);

    [LoggerMessage(EventId = 3001, EventName = "SwiftQueryStarted", Level = LogLevel.Information,
        Message = "Querying Swift messages from {FromUtc} to {ToUtc}")]
    public static partial void SwiftQueryStarted(ILogger logger, DateTime fromUtc, DateTime toUtc);

    [LoggerMessage(EventId = 3002, EventName = "SwiftQueryModeSelected", Level = LogLevel.Information,
        Message = "Using Swift query mode {QueryMode}")]
    public static partial void SwiftQueryModeSelected(ILogger logger, string queryMode);

    [LoggerMessage(EventId = 3003, EventName = "SwiftQueryCompleted", Level = LogLevel.Information,
        Message = "Swift query completed with {MessageCount} messages")]
    public static partial void SwiftQueryCompleted(ILogger logger, int messageCount);

    [LoggerMessage(EventId = 3004, EventName = "InvalidWarehouseId", Level = LogLevel.Warning,
        Message = "Skipping a Swift message with an absent or overlong WarehouseId")]
    public static partial void InvalidWarehouseId(ILogger logger);

    [LoggerMessage(EventId = 3005, EventName = "CollectionLengthMismatch", Level = LogLevel.Warning,
        Message = "Swift message {WarehouseId} has different collection lengths ({CollectionLengths}); missing positions will be NULL")]
    public static partial void CollectionLengthMismatch(ILogger logger, string warehouseId,
        string collectionLengths);

    [LoggerMessage(EventId = 3006, EventName = "MessageUnroutable", Level = LogLevel.Warning,
        Message = "Swift message {WarehouseId} is unroutable: {RoutingError}")]
    public static partial void MessageUnroutable(ILogger logger, string warehouseId, string routingError);

    [LoggerMessage(EventId = 3007, EventName = "SyncCompleted", Level = LogLevel.Information,
        Message = "Swift synchronization completed. Read={Read}, Inserted={Inserted}, Skipped={Skipped}, RoutingIssues={RoutingIssues}, Watermark={WatermarkUtc}, CorrelationId={CorrelationId}")]
    public static partial void SyncCompleted(ILogger logger, int read, int inserted, int skipped,
        int routingIssues, DateTime watermarkUtc, string correlationId);

    [LoggerMessage(EventId = 3099, EventName = "SyncFailed", Level = LogLevel.Critical,
        Message = "Swift synchronization failed; correlation {CorrelationId}")]
    public static partial void SyncFailed(ILogger logger, Exception exception, string correlationId);
}
