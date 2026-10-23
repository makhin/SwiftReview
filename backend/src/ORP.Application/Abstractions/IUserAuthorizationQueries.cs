namespace ORP.Application.Abstractions;

public sealed record UserIdentity(int UserId, string UserName, string DisplayName, bool IsGlobalAdministrator);
public sealed record UserPermissionCheck(bool IsGlobalAdministrator, bool CanView, bool HasPermission);

public sealed record UserPermissionSet(bool IsGlobalAdministrator, IReadOnlyList<string> Permissions)
{
    public UserPermissionCheck Check(string permission) => new(IsGlobalAdministrator,
        Permissions.Contains(ORP.Domain.Identity.Permissions.MessageView), Permissions.Contains(permission));
}

public interface IUserAuthorizationQueries
{
    Task<UserIdentity?> GetIdentityAsync(int userId, CancellationToken ct);
    Task<UserIdentity?> GetIdentityAsync(string userName, CancellationToken ct);
    Task<UserPermissionSet?> GetPermissionsAsync(int userId, int branchId, IReadOnlyList<int> departmentIds, CancellationToken ct);
    Task<UserPermissionCheck?> CheckAsync(int userId, int branchId, IReadOnlyList<int> departmentIds, string permission, CancellationToken ct);
}
