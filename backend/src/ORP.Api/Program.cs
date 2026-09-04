using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using ORP.Api.Authentication;
using ORP.Api.Authorization;
using ORP.Api.Endpoints;
using ORP.Api.Errors;
using ORP.Api.Infrastructure;
using ORP.Application;
using ORP.Application.Abstractions;
using ORP.Infrastructure;
using ORP.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICorrelationContext, CorrelationContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, MessageActionAuthorizationHandler>();
builder.Services.AddAuthentication("Debug").AddScheme<AuthenticationSchemeOptions, DebugAuthenticationHandler>("Debug", _ => { });
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddHealthChecks().AddDbContextCheck<ORPDbContext>();
builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("ORP.Api"))
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
app.MapScalarApiReference("/scalar", options => options.WithTitle("ORP API"));
app.MapApiEndpoints();

if (app.Configuration.GetValue<bool>("UseMockData"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
    await db.Database.EnsureCreatedAsync();
    await MockDataSeeder.SeedAsync(db);
}
else if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("BootstrapDatabase"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ORPDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
