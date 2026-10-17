using Microsoft.Extensions.Logging;

namespace ORP.Importer;

internal static partial class ImporterLog
{
    [LoggerMessage(EventId = 3008, EventName = "DirectionDefaulted", Level = LogLevel.Information,
        Message = "Using configured default direction {Direction} for SAA file {FilePath}")]
    public static partial void DirectionDefaulted(ILogger logger, string filePath, SaaMessageDirection direction);

    [LoggerMessage(EventId = 3004, EventName = "BranchResolved", Level = LogLevel.Debug,
        Message = "Resolved BranchId {BranchId} for SAA file {FilePath}")]
    public static partial void BranchResolved(ILogger logger, string filePath, int branchId);

    [LoggerMessage(EventId = 3005, EventName = "BranchResolutionFailed", Level = LogLevel.Warning,
        Message = "Could not resolve BranchId for SAA file {FilePath}; branch {Branch}: {Reason}")]
    public static partial void BranchResolutionFailed(ILogger logger, string filePath, string branch, string reason);

    [LoggerMessage(EventId = 3003, EventName = "DepartmentResolutionFailed", Level = LogLevel.Warning,
        Message = "Department rules could not resolve departments for SAA file {FilePath}: {Reason}")]
    public static partial void DepartmentResolutionFailed(ILogger logger, string filePath, string reason);

    [LoggerMessage(EventId = 3009, EventName = "DepartmentResolved", Level = LogLevel.Information,
        Message = "Resolved departments {DepartmentIds} for SAA file {FilePath} using rules {RuleIds}; branch {BranchId}, type {MessageType}, direction {Direction}")]
    public static partial void DepartmentResolved(ILogger logger, string filePath, string departmentIds, string ruleIds,
        int branchId, string messageType, SaaMessageDirection direction);

    [LoggerMessage(EventId = 3010, EventName = "DepartmentRuleNotMatched", Level = LogLevel.Information,
        Message = "No Department rule matched SAA file {FilePath}; branch {BranchId}, type {MessageType}, direction {Direction}")]
    public static partial void DepartmentRuleNotMatched(ILogger logger, string filePath, int branchId,
        string messageType, SaaMessageDirection direction);

    [LoggerMessage(EventId = 3000, EventName = "ParsingStarted", Level = LogLevel.Debug,
        Message = "Parsing SAA file {FilePath}")]
    public static partial void ParsingStarted(ILogger logger, string filePath);

    [LoggerMessage(EventId = 3001, EventName = "ParsingCompleted", Level = LogLevel.Debug,
        Message = "Parsed SAA file {FilePath}; hash {ContentHash}")]
    public static partial void ParsingCompleted(ILogger logger, string filePath, string contentHash);

    [LoggerMessage(EventId = 3002, EventName = "ParsingFailed", Level = LogLevel.Warning,
        Message = "Invalid SAA file {FilePath} at line {LineNumber}: {Reason}")]
    public static partial void ParsingFailed(ILogger logger, Exception exception, string filePath, int? lineNumber,
        string reason);
}
