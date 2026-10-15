using Microsoft.Extensions.Logging;

namespace ORP.Importer.ConsoleApp;

internal static partial class ConsoleImportLog
{
    [LoggerMessage(EventId = 3100, EventName = "ScanStarted", Level = LogLevel.Information,
        Message = "Scanning SAA directory {DirectoryPath}; retry ignored {RetryIgnored}")]
    public static partial void ScanStarted(ILogger logger, string directoryPath, bool retryIgnored);

    [LoggerMessage(EventId = 3101, EventName = "ScanCompleted", Level = LogLevel.Information,
        Message = "Scanned {Scanned}; imported {Imported}; already imported {Skipped}; ignored {Ignored}; already ignored {AlreadyIgnored}; errors {Failed}")]
    public static partial void ScanCompleted(ILogger logger, int scanned, int imported, int skipped,
        int ignored, int alreadyIgnored, int failed);

    [LoggerMessage(EventId = 3102, EventName = "FileProcessingFailed", Level = LogLevel.Warning,
        Message = "SAA file processing failed for {FilePath}: {Reason}")]
    public static partial void FileProcessingFailed(ILogger logger, Exception exception, string filePath, string reason);

    [LoggerMessage(EventId = 3103, EventName = "ImportStopped", Level = LogLevel.Error,
        Message = "SAA import stopped while processing {FilePath}")]
    public static partial void ImportStopped(ILogger logger, Exception exception, string filePath);

    [LoggerMessage(EventId = 3104, EventName = "DirectoryScanFailed", Level = LogLevel.Error,
        Message = "Cannot scan SAA directory {DirectoryPath}")]
    public static partial void DirectoryScanFailed(ILogger logger, Exception exception, string directoryPath);

    [LoggerMessage(EventId = 3105, EventName = "ConfigurationInvalid", Level = LogLevel.Error,
        Message = "Invalid importer configuration: {Reason}")]
    public static partial void ConfigurationInvalid(ILogger logger, string reason);
}
