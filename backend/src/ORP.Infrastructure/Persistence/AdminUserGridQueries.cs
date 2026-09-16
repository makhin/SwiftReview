using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;

namespace ORP.Infrastructure.Persistence;

public sealed record AdminUserGridRow
{
    public int Id { get; init; }
    public string UserName { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
}

public sealed class AdminUserGridQueries(ORPDbContext db)
{
    public Task<LoadResult> LoadAsync(DataSourceLoadOptionsBase options, string? search, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            query = query.Where(u => u.UserName.Contains(text) || u.DisplayName.Contains(text));
        }
        return DataSourceLoader.LoadAsync(query.Select(u => new AdminUserGridRow
        {
            Id = u.Id, UserName = u.UserName, DisplayName = u.DisplayName
        }), options, ct);
    }
}
