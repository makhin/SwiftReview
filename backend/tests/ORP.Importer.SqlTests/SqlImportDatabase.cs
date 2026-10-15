using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ORP.Domain.Identity;
using ORP.Domain.Messages;
using ORP.Domain.Workflows;
using ORP.Importer;
using ORP.Infrastructure.Persistence;

namespace ORP.Importer.SqlTests;

internal sealed class SqlImportDatabase : IAsyncDisposable
{
    private readonly string masterConnection;
    private readonly string name = "ORP_ImporterTests_" + Guid.NewGuid().ToString("N");
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "orp-sql-import-" + Guid.NewGuid().ToString("N"));
    public string ConnectionString { get; }
    public DbContextOptions<ORPDbContext> Options { get; }
    public SqlSaaImportStore Store => new(ConnectionString);
    public SaaMessageProcessor Processor { get; private set; } = null!;

    private SqlImportDatabase(string connection)
    {
        var builder = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
        masterConnection = builder.ConnectionString;
        builder.InitialCatalog = name;
        ConnectionString = builder.ConnectionString;
        Options = new DbContextOptionsBuilder<ORPDbContext>().UseSqlServer(ConnectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp")).Options;
    }

    public static async Task<SqlImportDatabase> CreateAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ORP_TEST_SQL_SERVER");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ORP_TEST_SQL_SERVER is required for Importer SQL tests; use test-backend.docker.sh.");
        var fixture = new SqlImportDatabase(connection);
        try
        {
            await using var master = new SqlConnection(fixture.masterConnection);
            await master.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{fixture.name}]", master);
            await command.ExecuteNonQueryAsync();
            Directory.CreateDirectory(fixture.Root);
            await using var db = new ORPDbContext(fixture.Options);
            await db.Database.MigrateAsync();
            var branch = new Branch("Paris");
            var department = new Department("CS");
            db.AddRange(branch, department);
            await db.SaveChangesAsync();
            db.WorkflowDefinitions.Add(new WorkflowDefinition(MessageDirection.Incoming, "CS MT199", "MT199", department.Id)
                .AddStep(1, 1));
            db.DepartmentRoutingRules.Add(new DepartmentRoutingRule
            {
                DepartmentId = department.Id, Direction = 1, MatchKind = 1, MessageTypePattern = "MT199",
                IsActive = true, Priority = 100, SourceFile = "test", SourceSheet = "rules", SourceRow = 1
            });
            await db.SaveChangesAsync();
            var catalog = new SaaRoutingCatalog(fixture.ConnectionString);
            fixture.Processor = new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(), await catalog.LoadBranchResolverAsync());
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public SaaSourceFileSnapshot File(string body = "content", string name = "sample.prt", string type = "MT199")
    {
        var path = Path.Combine(Root, name);
        System.IO.File.WriteAllText(path, string.Join("\n",
            $"Branch:Paris|MessageType:{type}|Reference#:test|DIRECTION:Incoming|SENDER:|RECEIVER:|Host:|Time:15/10/26 04:35:39 AM",
            "2026/10/15-04:35:30 PRINT 1", "________", "", "--- Instance Type and Transmission ---", body));
        return new SaaSourceFileReader().Read(path, Root);
    }

    public SaaProcessedMessage Route(SaaSourceFileSnapshot file) => Processor.Process(new SaaMessageParser().Parse(file));

    public async ValueTask DisposeAsync()
    {
        using var poolKey = new SqlConnection(ConnectionString);
        SqlConnection.ClearPool(poolKey);
        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync();
        await using var command = new SqlCommand($"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END", master);
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
    }
}
