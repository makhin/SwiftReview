using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SwiftReview.Infrastructure.Persistence;

public sealed class SwiftReviewDbContextFactory : IDesignTimeDbContextFactory<SwiftReviewDbContext>
{
    public SwiftReviewDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SwiftReviewDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=SwiftReview;User Id=sa;Password=SwiftReview_Strong_Passw0rd!;TrustServerCertificate=True")
            .Options;
        return new SwiftReviewDbContext(options);
    }
}
