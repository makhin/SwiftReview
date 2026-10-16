using Microsoft.EntityFrameworkCore;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class SeedTestDataTests
{
    [Fact]
    public async Task SeedCreatesMessagesAndDepartmentLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync();
        await using var db = new ORPDbContext(fixture.Options);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seed-test-data.sql"), ct);
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = script;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(ct);
        }
        Assert.Equal(100, await db.Messages.CountAsync(ct));
        Assert.Equal(100, await db.MessageDepartments.CountAsync(ct));
        Assert.Equal(4, await db.WorkflowDepartments.CountAsync(ct));
        Assert.Equal(100, await db.AuditEvents.CountAsync(ct));
        var clear = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "clear-imported-messages.sql"), ct);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = clear;
            await command.ExecuteNonQueryAsync(ct);
        }
        Assert.Empty(await db.Messages.ToListAsync(ct));
        Assert.Empty(await db.MessageDepartments.ToListAsync(ct));
        Assert.Empty(await db.SwiftMessages.ToListAsync(ct));
        Assert.Empty(await db.AuditEvents.ToListAsync(ct));
        Assert.Equal(4, await db.WorkflowDefinitions.CountAsync(ct));
        Assert.Equal(4, await db.WorkflowDepartments.CountAsync(ct));
        Assert.Equal(5, await db.Users.CountAsync(ct));
    }
}
