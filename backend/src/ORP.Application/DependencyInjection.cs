using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application.Assignments.Assign;
using ORP.Application.Assignments.Automatic;
using ORP.Application.Assignments.Reassign;
using ORP.Application.Audit.GetAuditTrail;
using ORP.Application.Dashboard.GetSummary;
using ORP.Application.Messages.Get;
using ORP.Application.Messages.Search;
using ORP.Application.Reviews;
using ORP.Application.ReferenceData;

namespace ORP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<MessageSearchValidator>();
        services.AddScoped<GetMessageHandler>();
        services.AddScoped<GetAuditTrailHandler>();
        services.AddScoped<GetDashboardSummaryHandler>();
        services.AddScoped<AssignMessageHandler>();
        services.AddScoped<ReassignMessageHandler>();
        services.AddScoped<AssignmentCoordinator>();
        services.AddScoped<AutomaticAssignmentService>();
        services.AddScoped<AssignNewMessageHandler>();
        services.AddScoped<StartReviewHandler>();
        services.AddScoped<ApproveReviewHandler>();
        services.AddScoped<RejectReviewHandler>();
        services.AddScoped<UndoReviewHandler>();
        services.AddScoped<SearchMessagesHandler>();
        services.AddScoped<GetWorkflowsHandler>();
        services.AddScoped<GetUsersHandler>();
        services.AddScoped<GetBranchesHandler>();
        services.AddScoped<GetDepartmentsHandler>();
        services.AddScoped<GetMessageTypesHandler>();
        services.AddScoped<GetMessageStatesHandler>();
        return services;
    }
}
