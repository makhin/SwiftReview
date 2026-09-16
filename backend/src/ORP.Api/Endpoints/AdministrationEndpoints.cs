using DevExtreme.AspNet.Data.ResponseModel;
using ORP.Api.Infrastructure;
using ORP.Application.Administration;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Endpoints;

public static class AdministrationEndpoints
{
    public static void MapAdministrationEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization("GlobalAdministrator").WithTags("Administration");
        admin.MapGet("/catalog", GetAccessCatalog)
            .WithName(nameof(GetAccessCatalog)).WithSummary("Get roles, permissions and access scope references.")
            .Produces<AccessCatalogDto>();
        admin.MapGet("/users/{id:int}/access", GetUserAccess)
            .WithName(nameof(GetUserAccess)).WithSummary("Get a user's role assignments and access scopes.")
            .Produces<UserAccessDetailsDto>().ProducesProblem(404);
        admin.MapPut("/users/{id:int}/access", UpdateUserAccess)
            .WithName(nameof(UpdateUserAccess)).WithSummary("Replace all scoped role assignments for a user.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        admin.MapPut("/roles/{id:int}/permissions", UpdateRolePermissions)
            .WithName(nameof(UpdateRolePermissions)).WithSummary("Replace the complete permission set for a role.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        admin.MapGet("/users/grid", GetAdminUsersGrid)
            .WithName(nameof(GetAdminUsersGrid)).WithSummary("Load users with paging, search and sorting.")
            .Produces<LoadResult>().ProducesProblem(400);
    }

    private static Task<AccessCatalogDto> GetAccessCatalog(IUserAdministrationService service, CancellationToken ct) =>
        service.GetCatalogAsync(ct);

    private static Task<UserAccessDetailsDto> GetUserAccess(int id, IUserAdministrationService service, CancellationToken ct) =>
        service.GetUserAsync(id, ct);

    private static async Task<IResult> UpdateUserAccess(int id, UpdateUserAccessRequest request,
        IUserAdministrationService service, CancellationToken ct)
    {
        await service.UpdateUserAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateRolePermissions(int id, UpdateRolePermissionsRequest request,
        IUserAdministrationService service, CancellationToken ct)
    {
        await service.UpdateRoleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static Task<LoadResult> GetAdminUsersGrid(AdminUserGridQueries queries, CancellationToken ct,
        int skip = 0, int take = 20, string? search = null, string? sort = null) =>
        queries.LoadAsync(AdminUserGridLoadOptions.Parse(skip, take, search, sort), search, ct);
}
