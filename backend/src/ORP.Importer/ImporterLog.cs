using Microsoft.Extensions.Logging;

namespace ORP.Importer;

internal static partial class ImporterLog
{
    [LoggerMessage(EventId = 3008, EventName = "DirectionDefaulted", Level = LogLevel.Information,
        Message = "Using configured default direction {Direction} for SAA file {FilePath}")]
    public static partial void DirectionDefaulted(ILogger logger, string filePath, SaaMessageDirection direction);

    [LoggerMessage(EventId = 3006, EventName = "MessageTypeResolved", Level = LogLevel.Debug,
        Message = "Resolved an allowed MessageType for SAA file {FilePath}")]
    public static partial void MessageTypeResolved(ILogger logger, string filePath);

    [LoggerMessage(EventId = 3007, EventName = "MessageTypeResolutionFailed", Level = LogLevel.Warning,
        Message = "Could not resolve an allowed MessageType for SAA file {FilePath}")]
    public static partial void MessageTypeResolutionFailed(ILogger logger, string filePath);

    [LoggerMessage(EventId = 3004, EventName = "BranchResolved", Level = LogLevel.Debug,
        Message = "Resolved BranchId {BranchId} for SAA file {FilePath}")]
    public static partial void BranchResolved(ILogger logger, string filePath, int branchId);

    [LoggerMessage(EventId = 3005, EventName = "BranchResolutionFailed", Level = LogLevel.Warning,
        Message = "Could not resolve BranchId for SAA file {FilePath}")]
    public static partial void BranchResolutionFailed(ILogger logger, string filePath);

    [LoggerMessage(EventId = 3003, EventName = "DepartmentResolutionFailed", Level = LogLevel.Warning,
        Message = "Department rules could not resolve DepartmentId for SAA file {FilePath}")]
    public static partial void DepartmentResolutionFailed(ILogger logger, string filePath);

    [LoggerMessage(EventId = 3000, EventName = "ParsingStarted", Level = LogLevel.Debug,
        Message = "Parsing SAA file {FilePath}")]
    public static partial void ParsingStarted(ILogger logger, string filePath);

    [LoggerMessage(EventId = 3001, EventName = "ParsingCompleted", Level = LogLevel.Debug,
        Message = "Parsed SAA file {FilePath}; hash {ContentHash}")]
    public static partial void ParsingCompleted(ILogger logger, string filePath, string contentHash);

    [LoggerMessage(EventId = 3002, EventName = "ParsingFailed", Level = LogLevel.Warning,
        Message = "Invalid SAA file {FilePath} at line {LineNumber}")]
    public static partial void ParsingFailed(ILogger logger, Exception exception, string filePath, int? lineNumber);
}
