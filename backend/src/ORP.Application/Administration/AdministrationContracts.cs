using System.Text.Json.Serialization;
using ORP.Application.Abstractions;

namespace ORP.Application.Administration;

public sealed record RoleDetailsDto(int Id, string Name, IReadOnlyList<string> Permissions);
public sealed record AccessCatalogDto(IReadOnlyList<RoleDetailsDto> Roles, IReadOnlyList<string> Permissions,
    IReadOnlyList<ReferenceItemDto> Branches, IReadOnlyList<ReferenceItemDto> Departments);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopedRoleAssignmentDto(int BranchId, int DepartmentId, IReadOnlyList<int> RoleIds);
public sealed record UserAccessDetailsDto(int UserId, string UserName, string DisplayName,
    IReadOnlyList<ScopedRoleAssignmentDto> Assignments, IReadOnlyList<UserScopeAccess> Scopes);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateUserAccessRequest(IReadOnlyList<ScopedRoleAssignmentDto> Assignments);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> Permissions);

public interface IUserAdministrationService
{
    Task<AccessCatalogDto> GetCatalogAsync(CancellationToken ct);
    Task<UserAccessDetailsDto> GetUserAsync(int userId, CancellationToken ct);
    Task UpdateUserAsync(int userId, UpdateUserAccessRequest request, CancellationToken ct);
    Task UpdateRoleAsync(int roleId, UpdateRolePermissionsRequest request, CancellationToken ct);
}
