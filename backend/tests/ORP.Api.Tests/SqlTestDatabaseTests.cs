using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ORP.Infrastructure;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class SqlTestDatabaseTests
{
    [Fact]
    public async Task Factories_UseIndependentDatabases_AndDisposeDeletesThem()
    {
        var first = new MessageApiFactory();
        var second = new MessageApiFactory();
        string firstConnection;
        string secondConnection;
        try
        {
            await first.InitializeAsync();
            await second.InitializeAsync();
            await using var firstScope = first.Services.CreateAsyncScope();
            await using var secondScope = second.Services.CreateAsyncScope();
            var firstDb = firstScope.ServiceProvider.GetRequiredService<ORPDbContext>();
            var secondDb = secondScope.ServiceProvider.GetRequiredService<ORPDbContext>();
            firstConnection = firstDb.Database.GetConnectionString()!;
            secondConnection = secondDb.Database.GetConnectionString()!;
            Assert.True(firstDb.Database.IsSqlServer());
            Assert.NotEqual(firstConnection, secondConnection);
            var user = first.UserId("amelia.hart");
            await firstDb.UserRoles.Where(r => r.UserId == user).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            var otherUser = second.UserId("amelia.hart");
            Assert.True(await secondDb.UserRoles.AnyAsync(r => r.UserId == otherUser, TestContext.Current.CancellationToken));
        }
        finally
        {
            try { await first.DisposeAsync(); }
            finally { await second.DisposeAsync(); }
        }
        Assert.False(await ExistsAsync(firstConnection));
        Assert.False(await ExistsAsync(secondConnection));
    }

    [Fact]
    public async Task FailedSeed_DeletesPartiallyInitializedDatabase()
    {
        await using var database = new SqlTestDatabase();
        await Assert.ThrowsAsync<SqlException>(() => database.InitializeAsync(TestContext.Current.CancellationToken,
            "THROW 51000, 'Injected seed failure', 1;"));
        Assert.False(await ExistsAsync(database.ConnectionString));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingConnection_FailsWithConfigurationInstruction(string? connection)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ORP"] = connection
        }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(configuration));
        Assert.Contains("ConnectionStrings:ORP", error.Message);
    }

    private static async Task<bool> ExistsAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
        command.Parameters.AddWithValue("@name", databaseName);
        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! != 0;
    }
}
