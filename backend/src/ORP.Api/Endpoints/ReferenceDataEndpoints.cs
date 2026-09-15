using ORP.Application.Abstractions;
using ORP.Application.ReferenceData;

namespace ORP.Api.Endpoints;

public static class ReferenceDataEndpoints
{
    public static void MapReferenceDataEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("").WithTags("ReferenceData");
        group.MapGet("/workflows", GetWorkflows)
            .WithName(nameof(GetWorkflows)).WithSummary("Get accessible workflows.")
            .Produces<IReadOnlyList<WorkflowSummaryDto>>();
        group.MapGet("/users", GetUsers)
            .WithName(nameof(GetUsers)).WithSummary("Get accessible users.")
            .Produces<IReadOnlyList<UserSummaryDto>>();
        group.MapGet("/branches", GetBranches)
            .WithName(nameof(GetBranches)).WithSummary("Get accessible branches.")
            .Produces<IReadOnlyList<ReferenceItemDto>>();
        group.MapGet("/departments", GetDepartments)
            .WithName(nameof(GetDepartments)).WithSummary("Get accessible departments.")
            .Produces<IReadOnlyList<ReferenceItemDto>>();
        group.MapGet("/message-types", GetMessageTypes)
            .WithName(nameof(GetMessageTypes)).WithSummary("Get accessible message-types.")
            .Produces<IReadOnlyList<string>>();
        group.MapGet("/message-states", GetMessageStates)
            .WithName(nameof(GetMessageStates)).WithSummary("Get accessible message-states.")
            .Produces<IReadOnlyList<MessageStateReferenceDto>>();
    }

    private static Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflows(GetWorkflowsHandler handler, CancellationToken ct) => handler.HandleAsync(ct);

    private static Task<IReadOnlyList<UserSummaryDto>> GetUsers(GetUsersHandler handler, CancellationToken ct) => handler.HandleAsync(ct);

    private static Task<IReadOnlyList<ReferenceItemDto>> GetBranches(GetBranchesHandler handler, CancellationToken ct) => handler.HandleAsync(ct);

    private static Task<IReadOnlyList<ReferenceItemDto>> GetDepartments(GetDepartmentsHandler handler, CancellationToken ct) => handler.HandleAsync(ct);

    private static Task<IReadOnlyList<string>> GetMessageTypes(GetMessageTypesHandler handler, CancellationToken ct) => handler.HandleAsync(ct);

    private static Task<IReadOnlyList<MessageStateReferenceDto>> GetMessageStates(GetMessageStatesHandler handler, CancellationToken ct) => handler.HandleAsync(ct);
}
