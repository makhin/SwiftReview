using SwiftReview.Application.Abstractions;

namespace SwiftReview.Api.Infrastructure;

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
        var id = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        CorrelationContext.Set(id); context.Response.Headers["X-Correlation-ID"] = id;
        System.Diagnostics.Activity.Current?.SetTag("correlation.id", id);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id });
        try { await next(context); }
        finally { CorrelationContext.Clear(); }
    }
}
