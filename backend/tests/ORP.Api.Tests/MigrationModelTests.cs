using Microsoft.EntityFrameworkCore;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class MigrationModelTests
{
    [Fact]
    public void InitialMigrationSnapshot_MatchesSqlServerModel()
    {
        var options = new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer("Server=localhost;Database=ORPModelCheck;Integrated Security=true").Options;
        using var db = new ORPDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
