using ORP.Application.Abstractions;
using ORP.Application.Dashboard.GetSummary;

namespace ORP.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/dashboard").WithTags("Dashboard");
        group.MapGet("/summary", GetDashboardSummary)
            .WithName(nameof(GetDashboardSummary)).WithSummary("Get an accessible message dashboard summary.")
            .Produces<DashboardSummaryDto>();
    }

    private static async Task<DashboardSummaryDto> GetDashboardSummary(GetDashboardSummaryHandler handler, CancellationToken ct) => await handler.HandleAsync(ct);
}
