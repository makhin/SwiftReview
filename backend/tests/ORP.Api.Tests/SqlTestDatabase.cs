using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Tests;

internal sealed class SqlTestDatabase : IAsyncDisposable
{
    public string ConnectionString { get; }
    private bool disposed;

    public SqlTestDatabase()
    {
        var configured = Environment.GetEnvironmentVariable("ORP_TEST_SQL_SERVER");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("Set ORP_TEST_SQL_SERVER to a SQL Server connection with permission to create and delete temporary test databases.");
        ConnectionString = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = $"ORP_Tests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    public ORPDbContext CreateContext() => new(new DbContextOptionsBuilder<ORPDbContext>()
        .UseSqlServer(ConnectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp");
            sql.EnableRetryOnFailure();
        }).Options);

    public async Task InitializeAsync(CancellationToken ct, string? seedSql = null)
    {
        try
        {
            await using var db = CreateContext();
            await db.Database.MigrateAsync(ct);
            var sql = seedSql ?? await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "seed-test-data.sql"), ct);
            await db.Database.OpenConnectionAsync(ct);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync(CancellationToken.None);
        disposed = true;
    }
}
