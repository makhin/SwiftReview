using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SwiftReview.Infrastructure.Persistence;

public sealed class SwiftReviewDbContextFactory : IDesignTimeDbContextFactory<SwiftReviewDbContext>
{
    public SwiftReviewDbContext CreateDbContext(string[] args)
    {
        DotNetEnv.Env.NoClobber().TraversePath().Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SwiftReview")
            ?? throw new InvalidOperationException("Connection string 'SwiftReview' is required.");
        var options = new DbContextOptionsBuilder<SwiftReviewDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new SwiftReviewDbContext(options);
    }
}
