using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Abstractions;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Persistence;
using ORP.Infrastructure.Services;

namespace ORP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        if (bool.TryParse(configuration["UseMockData"], out var useMockData) && useMockData)
        {
            var databaseName = $"ORPMock-{Guid.NewGuid():N}";
            services.AddDbContext<ORPDbContext>(options => options.UseInMemoryDatabase(databaseName));
        }
        else
        {
            var connection = configuration.GetConnectionString("ORP") ?? throw new InvalidOperationException("Connection string 'ORP' is required.");
            services.AddDbContext<ORPDbContext>(options => options.UseSqlServer(connection, sql =>
                sql.EnableRetryOnFailure()));
        }
        services.AddScoped<IORPStore, ORPStore>();
        services.AddScoped<IMessageQueries, MessageQueries>();
        services.AddScoped<MessageGridQueries>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IReferenceDataQueries, ReferenceDataQueries>();
        services.AddScoped<IWorkflowResolver, WorkflowResolver>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
