using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SwiftReview.Application.Assignments.Assign;
using SwiftReview.Application.Assignments.Reassign;
using SwiftReview.Application.Audit.GetAuditTrail;
using SwiftReview.Application.Dashboard.GetSummary;
using SwiftReview.Application.Messages.Get;
using SwiftReview.Application.Messages.Import;
using SwiftReview.Application.Messages.Search;
using SwiftReview.Application.Reviews;
using SwiftReview.Application.ReferenceData;

namespace SwiftReview.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ImportMessageValidator>();
        services.AddScoped<ImportMessageHandler>();
        services.AddScoped<GetMessageHandler>();
        services.AddScoped<GetAuditTrailHandler>();
        services.AddScoped<GetDashboardSummaryHandler>();
        services.AddScoped<AssignMessageHandler>();
        services.AddScoped<ReassignMessageHandler>();
        services.AddScoped<StartReviewHandler>();
        services.AddScoped<ApproveReviewHandler>();
        services.AddScoped<RejectReviewHandler>();
        services.AddScoped<UndoReviewHandler>();
        services.AddScoped<SearchMessagesHandler>();
        services.AddScoped<GetWorkflowsHandler>();
        services.AddScoped<GetUsersHandler>();
        return services;
    }
}
