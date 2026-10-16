using Microsoft.EntityFrameworkCore;
using ORP.Importer;

namespace ORP.Infrastructure.Persistence;

/// <summary>Loads one startup snapshot of routing reference data.</summary>
public sealed class SaaRoutingCatalog(string connectionString)
{
    private readonly DbContextOptions<ORPDbContext> options = new DbContextOptionsBuilder<ORPDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp"))
        .Options;

    public async Task<SaaMessageBranchResolver> LoadBranchResolverAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new ORPDbContext(options);
        var branches = await db.Branches.AsNoTracking()
            .Select(branch => new { branch.Name, branch.Id })
            .ToListAsync(cancellationToken);
        return new SaaMessageBranchResolver(branches.Select(branch =>
            new KeyValuePair<string, int>(branch.Name, branch.Id)));
    }

    public async Task<SaaDepartmentRuleResolver> LoadDepartmentResolverAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new ORPDbContext(options);
        var rules = await db.DepartmentRoutingRules.AsNoTracking().Where(rule => rule.IsActive)
            .Select(rule => new SaaDepartmentRule
            {
                Id = rule.Id, DepartmentId = rule.DepartmentId, BranchId = rule.BranchId,
                Direction = (SaaMessageDirection)rule.Direction, MatchKind = (SaaDepartmentMatchKind)rule.MatchKind,
                MessageTypePattern = rule.MessageTypePattern, IsActive = true
            }).ToListAsync(cancellationToken);
        return new SaaDepartmentRuleResolver(rules);
    }

}
