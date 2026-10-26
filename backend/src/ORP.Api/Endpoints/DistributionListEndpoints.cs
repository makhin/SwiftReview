using ORP.Application.DistributionLists;

namespace ORP.Api.Endpoints;

public static class DistributionListEndpoints
{
    public static void MapDistributionListEndpoints(this RouteGroupBuilder api)
    {
        var lists = api.MapGroup("/departments/{departmentId:int}/distribution-lists").WithTags("Distribution lists");
        lists.ProducesProblem(404);
        lists.MapGet("/", (int departmentId, IDistributionListService service, CancellationToken ct) =>
                service.GetAllAsync(departmentId, ct))
            .WithName("GetDistributionLists").WithSummary("Get the department's distribution lists and email addresses.")
            .Produces<IReadOnlyList<DistributionListDto>>();
        lists.MapGet("/{id:int}", (int departmentId, int id, IDistributionListService service, CancellationToken ct) =>
                service.GetAsync(departmentId, id, ct))
            .WithName("GetDistributionList").WithSummary("Get a distribution list and its email addresses.")
            .Produces<DistributionListDto>();
        lists.MapPost("/", async (int departmentId, SaveDistributionListRequest request,
                IDistributionListService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(departmentId, request, ct);
                return Results.Created($"/api/departments/{departmentId}/distribution-lists/{result.Id}", result);
            })
            .WithName("CreateDistributionList").WithSummary("Create a distribution list in a department.")
            .Produces<DistributionListDto>(201).ProducesProblem(400).ProducesProblem(409);
        lists.MapPut("/{id:int}", (int departmentId, int id, SaveDistributionListRequest request,
                IDistributionListService service, CancellationToken ct) => service.UpdateAsync(departmentId, id, request, ct))
            .WithName("UpdateDistributionList").WithSummary("Replace a distribution list's name and all email addresses.")
            .Produces<DistributionListDto>().ProducesProblem(400).ProducesProblem(409);
        lists.MapDelete("/{id:int}", async (int departmentId, int id, IDistributionListService service, CancellationToken ct) =>
            {
                await service.DeleteAsync(departmentId, id, ct);
                return Results.NoContent();
            })
            .WithName("DeleteDistributionList").WithSummary("Delete a distribution list and its email addresses.")
            .Produces(204).ProducesProblem(409);
    }
}
