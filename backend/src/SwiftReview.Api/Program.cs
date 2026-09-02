using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using SwiftReview.Api.Authentication;
using SwiftReview.Api.Authorization;
using SwiftReview.Api.Endpoints;
using SwiftReview.Api.Errors;
using SwiftReview.Api.Hubs;
using SwiftReview.Api.Infrastructure;
using SwiftReview.Application;
using SwiftReview.Application.Abstractions;
using SwiftReview.Infrastructure;
using SwiftReview.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<InternalEventDeduplicator>();
builder.Services.AddSingleton<ICorrelationContext, CorrelationContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, MessageActionAuthorizationHandler>();
builder.Services.AddAuthentication("Debug").AddScheme<AuthenticationSchemeOptions, DebugAuthenticationHandler>("Debug", _ => { });
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddDbContextCheck<SwiftReviewDbContext>();
builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("SwiftReview.Api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddEntityFrameworkCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) t.AddOtlpExporter();
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) m.AddOtlpExporter();
    });

var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapScalarApiReference("/scalar", options => options.WithTitle("SwiftReview API"));
app.MapHub<MessagesHub>("/hubs/messages");
app.MapApiEndpoints();

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("BootstrapDatabase"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SwiftReviewDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
