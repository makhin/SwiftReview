using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ORP.Infrastructure.Persistence;

public sealed class ORPDbContextFactory : IDesignTimeDbContextFactory<ORPDbContext>
{
    public ORPDbContext CreateDbContext(string[] args)
    {
        DotNetEnv.Env.NoClobber().TraversePath().Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ORP")
            ?? throw new InvalidOperationException("Connection string 'ORP' is required.");
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ORPDbContext(options);
    }
}
