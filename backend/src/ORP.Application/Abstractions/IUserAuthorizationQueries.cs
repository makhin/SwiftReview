namespace ORP.Application.Abstractions;

public sealed record UserIdentity(int UserId, string UserName, string DisplayName, bool IsGlobalAdministrator);
public sealed record UserPermissionCheck(bool IsGlobalAdministrator, bool CanView, bool HasPermission);

public interface IUserAuthorizationQueries
{
    Task<UserIdentity?> GetIdentityAsync(int userId, CancellationToken ct);
    Task<UserIdentity?> GetIdentityAsync(string userName, CancellationToken ct);
    Task<UserPermissionCheck?> CheckAsync(int userId, int branchId, int departmentId, string permission, CancellationToken ct);
    Task<bool> CanAccessWorkflowAsync(int userId, int workflowId, CancellationToken ct);
}
