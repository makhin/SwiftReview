using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SwiftReview.Application;
using SwiftReview.Application.Abstractions;
using SwiftReview.Infrastructure;
using SwiftReview.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ICorrelationContext, WorkerCorrelationContext>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpClient<IRealtimeNotifier, ApiRealtimeNotifier>(client => client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080"))
    .AddStandardResilienceHandler();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<AwhIngestionWorker>();
builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("SwiftReview.Worker"))
    .WithTracing(t =>
    {
        t.AddHttpClientInstrumentation().AddEntityFrameworkCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) t.AddOtlpExporter();
    })
    .WithMetrics(m =>
    {
        m.AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) m.AddOtlpExporter();
    });
await builder.Build().RunAsync();
