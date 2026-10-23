using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.Tests;

public sealed class MigrationMetadataTests
{
    [Fact]
    public void LatestMigrationTargetModelMatchesSnapshot()
    {
        using var db = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer("Server=localhost;Database=MetadataOnly;Integrated Security=True")
            .Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(migrations.Migrations.Last().Value, db.Database.ProviderName!);
        var initializer = db.GetService<IModelRuntimeInitializer>();
        var target = initializer.Initialize(migration.TargetModel, designTime: true);
        var snapshot = initializer.Initialize(migrations.ModelSnapshot!.Model, designTime: true);

        Assert.NotEmpty(target.GetEntityTypes());
        Assert.False(db.GetService<IMigrationsModelDiffer>().HasDifferences(
            target.GetRelationalModel(), snapshot.GetRelationalModel()));
    }
}
