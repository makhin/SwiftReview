using System.Text.Json;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using ORP.Application.Administration;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Endpoints;

public sealed record AdminUserGridRow(int Id, string UserName, string DisplayName);

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").RequireAuthorization("GlobalAdministrator");
        admin.MapGet("/catalog", (IUserAdministrationService service, CancellationToken ct) => service.GetCatalogAsync(ct));
        admin.MapGet("/users/{id:int}/access", (int id, IUserAdministrationService service, CancellationToken ct) => service.GetUserAsync(id, ct));
        admin.MapPut("/users/{id:int}/access", async (int id, UpdateUserAccessRequest request, IUserAdministrationService service, CancellationToken ct) =>
        { await service.UpdateUserAsync(id, request, ct); return Results.NoContent(); });
        admin.MapPut("/roles/{id:int}/permissions", async (int id, UpdateRolePermissionsRequest request, IUserAdministrationService service, CancellationToken ct) =>
        { await service.UpdateRoleAsync(id, request, ct); return Results.NoContent(); });
        admin.MapGet("/users/grid", Users).Produces<LoadResult>();
        return endpoints;
    }

    private static Task<LoadResult> Users(ORPDbContext db, CancellationToken ct, int skip = 0, int take = 20,
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
        return DataSourceLoader.LoadAsync(query.Select(u => new AdminUserGridRow(u.Id, u.UserName, u.DisplayName)),
            new DataSourceLoadOptionsBase { Skip = skip, Take = take, RequireTotalCount = true,
                Sort = sorts.Count == 0 ? [new SortingInfo { Selector = "DisplayName" }] : sorts.ToArray(),
                PrimaryKey = ["Id"], SortByPrimaryKey = true }, ct);
    }
}
