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
        if (bool.TryParse(configuration["UseMockData"], out var useMockData) && useMockData)
        {
            var databaseName = $"SwiftReviewMock-{Guid.NewGuid():N}";
            services.AddDbContext<SwiftReviewDbContext>(options => options.UseInMemoryDatabase(databaseName));
        }
        else
        {
            var connection = configuration.GetConnectionString("SwiftReview") ?? throw new InvalidOperationException("Connection string 'SwiftReview' is required.");
            services.AddDbContext<SwiftReviewDbContext>(options => options.UseSqlServer(connection, sql =>
                sql.EnableRetryOnFailure()));
        }
        services.AddScoped<ISwiftReviewStore, SwiftReviewStore>();
        services.AddScoped<IMessageQueries, MessageQueries>();
        services.AddScoped<MessageGridQueries>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IReferenceDataQueries, ReferenceDataQueries>();
        services.AddScoped<IWorkflowResolver, WorkflowResolver>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
