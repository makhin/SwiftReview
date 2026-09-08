using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ORP.Api.Infrastructure;

internal static partial class ApiLog
{
    public static int? UserId(HttpContext context) =>
        int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    public static string CorrelationId(HttpContext context) =>
        context.Items[CorrelationMiddleware.CorrelationIdItemKey] as string
        ?? System.Diagnostics.Activity.Current?.TraceId.ToString()
        ?? "system";

    [LoggerMessage(EventId = 1000, EventName = "RequestCompleted",
        Message = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms for user {UserId}; correlation {CorrelationId}")]
    public static partial void RequestCompleted(ILogger logger, LogLevel level, string requestMethod,
        string requestPath, int statusCode, double elapsedMilliseconds, int? userId, string correlationId);

    [LoggerMessage(EventId = 1010, EventName = "AuthorizationDenied", Level = LogLevel.Warning,
        Message = "Authorization denied for HTTP {RequestMethod} {RequestPath} with status {StatusCode} for user {UserId}; correlation {CorrelationId}")]
    public static partial void AuthorizationDenied(ILogger logger, string requestMethod, string requestPath,
        int statusCode, int? userId, string correlationId);

    [LoggerMessage(EventId = 1011, EventName = "MessageAuthorizationDenied", Level = LogLevel.Warning,
        Message = "Message action authorization denied for message {MessageId}, user {UserId}, permission {Permission}, review level {ReviewLevel}, ownership {Ownership}; permission={HasPermission}, branch={HasBranchAccess}, department={HasDepartmentAccess}, state={StateAllowed}, four-eyes={FourEyesAllowed}, owner={OwnershipAllowed}; correlation {CorrelationId}")]
    public static partial void MessageAuthorizationDenied(ILogger logger, long messageId, int? userId,
        string permission, int? reviewLevel, string ownership, bool hasPermission, bool hasBranchAccess,
        bool hasDepartmentAccess, bool stateAllowed, bool fourEyesAllowed, bool ownershipAllowed,
        string correlationId);

    [LoggerMessage(EventId = 1020, EventName = "ExpectedRequestFailed", Level = LogLevel.Warning,
        Message = "HTTP {RequestMethod} {RequestPath} failed with expected status {StatusCode} for user {UserId}; correlation {CorrelationId}")]
    public static partial void ExpectedRequestFailed(ILogger logger, Exception exception,
        string requestMethod, string requestPath, int statusCode, int? userId, string correlationId);

    [LoggerMessage(EventId = 1021, EventName = "UnexpectedRequestFailed", Level = LogLevel.Error,
        Message = "HTTP {RequestMethod} {RequestPath} failed unexpectedly for user {UserId}; correlation {CorrelationId}")]
    public static partial void UnexpectedRequestFailed(ILogger logger, Exception exception,
        string requestMethod, string requestPath, int? userId, string correlationId);

    [LoggerMessage(EventId = 1030, EventName = "DatabaseInitializationStarted", Level = LogLevel.Information,
        Message = "Database initialization started in {Mode} mode")]
    public static partial void DatabaseInitializationStarted(ILogger logger, string mode);

    [LoggerMessage(EventId = 1031, EventName = "DatabaseInitializationCompleted", Level = LogLevel.Information,
        Message = "Database initialization completed in {Mode} mode")]
    public static partial void DatabaseInitializationCompleted(ILogger logger, string mode);
}
