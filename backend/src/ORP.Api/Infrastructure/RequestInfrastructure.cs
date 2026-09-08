using System.Diagnostics;
using System.Security.Claims;
using ORP.Application.Abstractions;

namespace ORP.Api.Infrastructure;

public sealed class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();
    public string CorrelationId => Current.Value ?? "system";
    public static void Set(string value) => Current.Value = value;
    public static void Clear() => Current.Value = null;
}

public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    internal static readonly object CorrelationIdItemKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Response.OnCompleted(() =>
        {
            var userId = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : (int?)null;
            var statusCode = context.Response.StatusCode;
            var level = statusCode >= 500 ? LogLevel.Error
                : statusCode >= 400 ? LogLevel.Warning
                : LogLevel.Information;
            ApiLog.RequestCompleted(logger, level, context.Request.Method, context.Request.Path,
                statusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, userId, correlationId);
            return Task.CompletedTask;
        });

        var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (supplied?.Length > 100)
            throw new BadHttpRequestException("X-Correlation-ID cannot exceed 100 characters.");
        correlationId = string.IsNullOrWhiteSpace(supplied) ? correlationId : supplied;
        // Exception handling clears response headers, but request items survive.
        context.Items[CorrelationIdItemKey] = correlationId;
        CorrelationContext.Set(correlationId); context.Response.Headers["X-Correlation-ID"] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        try { await next(context); }
        finally { CorrelationContext.Clear(); }
    }
}
