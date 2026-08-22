using Microsoft.EntityFrameworkCore;
using SwiftReview.Application.Abstractions;
using SwiftReview.Infrastructure.Persistence;

namespace SwiftReview.Infrastructure.Identity;

public sealed class UserAccessService(SwiftReviewDbContext db) : IUserAccessService
{
    public async Task<UserAccess?> GetByUserNameAsync(string name, CancellationToken ct) => Map(await BaseQuery().SingleOrDefaultAsync(x => x.UserName == name, ct));
    public async Task<UserAccess?> GetByIdAsync(int id, CancellationToken ct) => Map(await BaseQuery().SingleOrDefaultAsync(x => x.Id == id, ct));

    private IQueryable<Domain.Identity.User> BaseQuery() => db.Users.AsNoTracking()
        .Include(x => x.Roles).ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
        .Include(x => x.Branches).Include(x => x.Departments).AsSplitQuery();
    private static UserAccess? Map(Domain.Identity.User? x) => x is null ? null : new UserAccess(x.Id, x.UserName,
        x.Roles.SelectMany(r => r.Role.Permissions.Select(p => p.Permission.Name)).ToHashSet(),
        x.Branches.Select(b => b.BranchId).ToHashSet(), x.Departments.Select(d => d.DepartmentId).ToHashSet());
}
