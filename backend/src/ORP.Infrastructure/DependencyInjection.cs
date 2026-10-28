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
        var connection = configuration.GetConnectionString("ORP");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Connection string 'ORP' is required. Configure ConnectionStrings:ORP for SQL Server.");
        services.AddDbContext<ORPDbContext>(options => options.UseSqlServer(connection, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp");
            sql.EnableRetryOnFailure();
        }));
        services.AddScoped<ORP.Infrastructure.Logging.BusinessActionLog>();
        services.AddScoped<ITransactionExecutor, TransactionExecutor>();
        services.AddScoped<IORPStore, ORPStore>();
        services.AddScoped<IMessageQueries, MessageQueries>();
        services.AddScoped<ORP.Application.Messages.Get.IMessageViewQueries, MessageViewQueries>();
        services.AddScoped<MessageGridQueries>();
        services.AddScoped<AdminUserGridQueries>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IUserAuthorizationQueries, UserAuthorizationQueries>();
        services.AddScoped<ORP.Application.Administration.IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<ORP.Application.DistributionLists.IDistributionListService, DistributionListService>();
        services.AddScoped<ORP.Application.Emails.ForwardingSubjectResolver>();
        services.AddScoped<ORP.Application.Emails.IForwardingSubjectService, ForwardingSubjectService>();
        services.AddScoped<ORP.Application.Emails.IOutgoingEmailService, OutgoingEmailService>();
        services.AddScoped<IAssignmentCandidateQueries, AssignmentCandidateQueries>();
        services.AddScoped<IReferenceDataQueries, ReferenceDataQueries>();
        services.AddScoped<IWorkflowResolver, WorkflowResolver>();
        services.AddSingleton<Documents.TextToPdfConverter>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
