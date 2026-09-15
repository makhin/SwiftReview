using ORP.Application.Abstractions;

namespace ORP.Api.Endpoints;

public static class CurrentUserEndpoints
{
    public static void MapCurrentUserEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("").WithTags("CurrentUser");
        group.MapGet("/me", GetCurrentUser)
            .WithName(nameof(GetCurrentUser)).WithSummary("Get the current user and access scopes.")
            .Produces<CurrentUserResponse>();
    }

    private static async Task<CurrentUserResponse> GetCurrentUser(ICurrentUser current, IUserAccessService users, CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        return new CurrentUserResponse(access.UserId, access.UserName, access.DisplayName,
            access.Permissions.Order().ToArray(), access.BranchIds.Order().ToArray(),
            access.DepartmentIds.Order().ToArray(), access.IsGlobalAdministrator, access.Scopes);
    }
}
