namespace ORP.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").RequireAuthorization();
        api.ProducesProblem(StatusCodes.Status401Unauthorized);
        api.ProducesProblem(StatusCodes.Status403Forbidden);
        api.MapMessageEndpoints();
        api.MapReviewEndpoints();
        api.MapReferenceDataEndpoints();
        api.MapDashboardEndpoints();
        api.MapCurrentUserEndpoints();
        api.MapAdministrationEndpoints();
        return endpoints;
    }
}
