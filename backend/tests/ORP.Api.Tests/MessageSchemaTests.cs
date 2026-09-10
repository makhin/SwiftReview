using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ORP.Domain.Messages;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class MessageSchemaTests
{
    [Fact]
    public void MessageModelAndInitialMigration_DoNotUseRowVersion()
    {
        using var db = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaTest;Integrated Security=true")
            .Options);

        var message = db.Model.FindEntityType(typeof(Message));
        Assert.NotNull(message);
        Assert.Null(message.FindProperty("RowVersion"));
        Assert.DoesNotContain(message.GetProperties(), property => property.IsConcurrencyToken);
        Assert.False(db.Database.HasPendingModelChanges());

        var sql = db.GetService<IMigrator>().GenerateScript();
        Assert.DoesNotContain("RowVersion", sql);
        Assert.DoesNotContain("rowversion", sql);
    }

    [Fact]
    public void SqlServerModelAndInitialMigration_DoNotContainEntries()
    {
        // Model and SQL generation only; no database connection is opened.
        using var db = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaTest;Integrated Security=true")
            .Options);

        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity =>
            entity.GetTableName() == "SwiftMessageEntries");
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Single(db.Database.GetMigrations());

        var sql = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE [orp].[SwiftMessages]", sql);
        Assert.DoesNotContain("SwiftMessageEntries", sql);
        Assert.DoesNotContain("[Account]", sql);
        Assert.DoesNotContain("[Amount]", sql);
        Assert.DoesNotContain("[Currency]", sql);
    }
}
