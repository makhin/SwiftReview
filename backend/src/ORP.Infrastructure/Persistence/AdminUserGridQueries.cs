using Microsoft.EntityFrameworkCore;
using ORP.Application.Abstractions;
using ORP.Application.Grids;

namespace ORP.Infrastructure.Persistence;

public sealed class AdminUserGridQueries(ORPDbContext db)
{
    public async Task<PagedResult<AdminUserGridRow>> LoadAsync(AdminUserGridRequest request, CancellationToken ct)
    {
        var options = GridQuery<AdminUserGridRow>.Create(request, GridFields.Users, 100, 3, new SortClause("displayName", "asc"));
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim();
            query = query.Where(u => u.UserName.Contains(text) || u.DisplayName.Contains(text));
        }
        var rows = options.Filter(query.Select(u => new AdminUserGridRow
        {
            Id = u.Id, UserName = u.UserName, DisplayName = u.DisplayName
        }));
        var count = await rows.CountAsync(ct);
        return new(await options.Page(rows).ToListAsync(ct), count);
    }
}
