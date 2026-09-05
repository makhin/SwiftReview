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
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (supplied?.Length > 100)
            throw new BadHttpRequestException("X-Correlation-ID cannot exceed 100 characters.");
        var id = string.IsNullOrWhiteSpace(supplied) ? Guid.NewGuid().ToString("N") : supplied;
        CorrelationContext.Set(id); context.Response.Headers["X-Correlation-ID"] = id;
        System.Diagnostics.Activity.Current?.SetTag("correlation.id", id);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id });
        try { await next(context); }
        finally { CorrelationContext.Clear(); }
    }
}
