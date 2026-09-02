using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftReview.Application.Abstractions;
using SwiftReview.Infrastructure.Identity;
using SwiftReview.Infrastructure.Persistence;
using SwiftReview.Infrastructure.Services;

namespace SwiftReview.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("SwiftReview") ?? throw new InvalidOperationException("Connection string 'SwiftReview' is required.");
        services.AddDbContext<SwiftReviewDbContext>(options => options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<ISwiftReviewStore, SwiftReviewStore>();
        services.AddScoped<IMessageQueries, MessageQueries>();
        services.AddScoped<MessageGridQueries>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IReferenceDataQueries, ReferenceDataQueries>();
        services.AddScoped<IWorkflowResolver, WorkflowResolver>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAwhClient, FakeAwhClient>();
        services.AddSingleton<IDocumentStorage, FakeDocumentStorage>();
        services.AddSingleton<INotificationSender, FakeNotificationSender>();
        services.AddHttpClient("AwhProductionAdapter").AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.Retry.MaxRetryAttempts = 3;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
