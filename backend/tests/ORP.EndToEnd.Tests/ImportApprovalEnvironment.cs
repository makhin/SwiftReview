using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ORP.Domain.Identity;
using ORP.Domain.Workflows;
using ORP.Importer;
using ORP.Infrastructure.Persistence;

namespace ORP.EndToEnd.Tests;

internal sealed class ImportApprovalEnvironment : IAsyncDisposable
{
    public const string Body = "\n--- Instance Type and Transmission ---\n:20:E2E-APPROVAL\n:79:Please review this message.\n";
    private readonly string name = "ORP_EndToEndTests_" + Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "orp-e2e-" + Guid.NewGuid().ToString("N"));
    private readonly string masterConnection;
    private readonly string connection;
    private ApiFactory? factory;
    private SaaFileImportService importer = null!;
    public DbContextOptions<ORPDbContext> Options { get; }
    public int FirstReviewerId { get; private set; }
    public int SecondReviewerId { get; private set; }
    public int BranchId { get; private set; }
    public int DepartmentId { get; private set; }
    public int WorkflowId { get; private set; }

    private ImportApprovalEnvironment(string server)
    {
        var builder = new SqlConnectionStringBuilder(server) { InitialCatalog = "master" };
        masterConnection = builder.ConnectionString;
        builder.InitialCatalog = name;
        connection = builder.ConnectionString;
        Options = new DbContextOptionsBuilder<ORPDbContext>().UseSqlServer(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp")).Options;
    }

    public static async Task<ImportApprovalEnvironment> CreateAsync(CancellationToken ct)
    {
        var server = Environment.GetEnvironmentVariable("ORP_TEST_SQL_SERVER");
        if (string.IsNullOrWhiteSpace(server))
            throw new InvalidOperationException("ORP_TEST_SQL_SERVER is required for EndToEnd tests; use test-backend.docker.sh.");
        var environment = new ImportApprovalEnvironment(server);
        try
        {
            await using var master = new SqlConnection(environment.masterConnection);
            await master.OpenAsync(ct);
            await using var command = new SqlCommand($"CREATE DATABASE [{environment.name}]", master);
            await command.ExecuteNonQueryAsync(ct);
            await environment.PrepareAsync(ct);
            return environment;
        }
        catch
        {
            await environment.DisposeAsync();
            throw;
        }
    }

    private async Task PrepareAsync(CancellationToken ct)
    {
        await using var db = new ORPDbContext(Options);
        await db.Database.MigrateAsync(ct);
        var branch = new Branch("Paris");
        var department = new Department("CS");
        var first = new User("e2e.first", "First reviewer");
        var second = new User("e2e.second", "Second reviewer");
        var role = new Role("E2E reviewers");
        string[] permissions = [Permissions.MessageView, Permissions.MessageAssign,
            Permissions.ReviewLevel1, Permissions.ReviewLevel2, Permissions.AuditView];
        foreach (var permission in await db.Permissions.Where(p => permissions.Contains(p.Name)).ToListAsync(ct))
            role.Permissions.Add(new RolePermission { Permission = permission });
        var workflow = new WorkflowDefinition("E2E_DOUBLE", "Two independent approvals").AddStep(1, 1).AddStep(2, 2);
        workflow.ConfigureConfirmation(ConfirmationMode.Immediate, false, AssignmentRetentionMode.PerConfirmation, false);
        db.AddRange(branch, department, first, second, role, workflow);
        await db.SaveChangesAsync(ct);
        BranchId = branch.Id;
        DepartmentId = department.Id;
        WorkflowId = workflow.Id;
        FirstReviewerId = first.Id;
        SecondReviewerId = second.Id;
        foreach (var user in new[] { first, second })
            db.UserRoles.Add(new UserRole { UserId = user.Id, BranchId = branch.Id, DepartmentId = department.Id, RoleId = role.Id });
        db.DepartmentRoutingRules.Add(new DepartmentRoutingRule
        {
            WorkflowDefinitionId = workflow.Id, DepartmentId = department.Id, Direction = 1,
            MatchKind = 1, MessageTypePattern = "MT199", IsActive = true,
            SourceFile = "e2e", SourceSheet = "rules", SourceRow = 1
        });
        await db.SaveChangesAsync(ct);

        var catalog = new SaaRoutingCatalog(connection);
        importer = new SaaFileImportService(new SaaSourceFileReader(), new SaaMessageParser(),
            new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(), await catalog.LoadBranchResolverAsync()),
            new SqlSaaImportStore(connection));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "approval.prt"),
            "Branch:Paris|MessageType:MT199|Reference#:E2E-APPROVAL|DIRECTION:Incoming|SENDER:SENDERXX|RECEIVER:RECEIVERXX|Host:E2E|Time:23/10/26 04:35:39 AM\n" +
            "2026/10/23-04:35:30 PRINT 1\n________\n" + Body, ct);
        factory = new ApiFactory(connection);
    }

    public Task<SaaFileImportResult> ImportAsync(CancellationToken ct) =>
        importer.ImportAsync(Path.Combine(root, "approval.prt"), root, cancellationToken: ct);

    public HttpClient Client(string user)
    {
        var client = factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", user);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (factory is not null) await factory.DisposeAsync();
        }
        finally
        {
            try
            {
                using var poolKey = new SqlConnection(connection);
                SqlConnection.ClearPool(poolKey);
                await using var master = new SqlConnection(masterConnection);
                await master.OpenAsync();
                await using var command = new SqlCommand($"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END", master);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    private sealed class ApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }
}
