using ORP.Application.Abstractions;
using ORP.Domain.Identity;

namespace ORP.Application.DistributionLists;

public static class DistributionListAccess
{
    public static bool CanManage(UserAccess access, int departmentId) => access.IsGlobalAdministrator ||
        access.Scopes.Any(s => s.DepartmentId == departmentId && s.Permissions.Contains(Permissions.DistributionListManage));

    public static bool CanRead(UserAccess access, int departmentId) => CanManage(access, departmentId) ||
        access.Scopes.Any(s => s.DepartmentId == departmentId && s.Permissions.Contains(Permissions.MessageForward));
}
