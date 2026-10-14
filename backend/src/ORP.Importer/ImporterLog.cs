using Microsoft.Extensions.Logging;

namespace ORP.Importer;

internal static partial class ImporterLog
{
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
