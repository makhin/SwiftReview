using System.Text.Json;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Administration;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Endpoints;

public sealed record AdminUserGridRow
{
    public int Id { get; init; }
    public string UserName { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
}

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

    private static Task<LoadResult> GetAdminUsersGrid(ORPDbContext db, CancellationToken ct, int skip = 0, int take = 20,
        string? search = null, string? sort = null)
    {
        if (skip < 0 || take is < 1 or > 100 || search?.Length > 100)
            throw new FormatException("Invalid user paging or search options.");
        var sorts = new List<SortingInfo>();
        if (!string.IsNullOrEmpty(sort))
        {
            try
            {
                using var json = JsonDocument.Parse(sort);
                if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() > 3)
                    throw new FormatException("At most three user sort fields are allowed.");
                foreach (var clause in json.RootElement.EnumerateArray())
                {
                    var field = clause.GetProperty("selector").GetString() switch
                    { "id" => "Id", "userName" => "UserName", "displayName" => "DisplayName", _ => throw new FormatException("Unknown user sort field.") };
                    sorts.Add(new SortingInfo { Selector = field, Desc = clause.TryGetProperty("desc", out var desc) && desc.GetBoolean() });
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            { throw new FormatException("Invalid user sort options.", ex); }
        }
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            query = query.Where(u => u.UserName.Contains(text) || u.DisplayName.Contains(text));
        }
        return DataSourceLoader.LoadAsync(query.Select(u => new AdminUserGridRow { Id = u.Id, UserName = u.UserName, DisplayName = u.DisplayName }),
            new DataSourceLoadOptionsBase { Skip = skip, Take = take, RequireTotalCount = true,
                Sort = sorts.Count == 0 ? [new SortingInfo { Selector = "DisplayName" }] : sorts.ToArray(),
                PrimaryKey = ["Id"], SortByPrimaryKey = true }, ct);
    }
}
