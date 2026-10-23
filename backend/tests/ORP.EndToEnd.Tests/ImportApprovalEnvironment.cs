using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ORP.Domain.Identity;
using ORP.Domain.Workflows;
using ORP.Importer;
using ORP.Infrastructure.Persistence;

namespace ORP.EndToEnd.Tests;

internal sealed class ImportApprovalEnvironment : IAsyncDisposable
{
    public const string Body = "\n--- Instance Type and Transmission ---\n:20:E2E-APPROVAL\n:79:Please review this message.\n";
    private readonly string runId = Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "orp-e2e-" + Guid.NewGuid().ToString("N"));
    private readonly string connection;
    private ApiFactory? factory;
    private SaaFileImportService importer = null!;
    public DbContextOptions<ORPDbContext> Options { get; }
    public int FirstReviewerId { get; private set; }
    public int SecondReviewerId { get; private set; }
    public int BranchId { get; private set; }
    public int DepartmentId { get; private set; }
    public int WorkflowId { get; private set; }
    public string BranchName => $"E2E-{runId}";
    public string DepartmentName => $"E2E-{runId}";
    public string Reference => $"E2E-{runId}";
    public string FileName => $"approval-{runId}.prt";
    public string MessageType { get; private set; } = null!;

    private ImportApprovalEnvironment(string server)
    {
        var builder = new SqlConnectionStringBuilder(server);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog) ||
            new[] { "master", "model", "msdb", "tempdb" }.Contains(builder.InitialCatalog, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Specify an existing application database in the connection string.");
        connection = builder.ConnectionString;
        Options = new DbContextOptionsBuilder<ORPDbContext>().UseSqlServer(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp")).Options;
    }

    public static async Task<ImportApprovalEnvironment> CreateAsync(CancellationToken ct)
    {
        // Test runners do not apply launchSettings.json. Default to Development
        // there too, while respecting an explicitly selected environment.
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        // Keep our standard appsettings filenames separate from the referenced
        // API's settings in the build output.
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "EndToEndSettings"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true);
        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            builder.AddJsonFile("appsettings.EndToEnd.local.json", optional: true)
                .AddUserSecrets<ImportApprovalEnvironment>(optional: true);
        using var configuration = (ConfigurationRoot)builder.AddEnvironmentVariables().Build();
        var server = configuration["ORP_TEST_SQL_SERVER"];
        if (string.IsNullOrWhiteSpace(server)) server = configuration.GetConnectionString("ORP");
        if (string.IsNullOrWhiteSpace(server))
            throw new InvalidOperationException("Configure ConnectionStrings:ORP in User Secrets (SwiftReview-ORP-Api), " +
                "appsettings.EndToEnd.local.json or ConnectionStrings__ORP; alternatively set ORP_TEST_SQL_SERVER " +
                "or use test-backend.docker.sh. The configured application database must already have the current schema.");
        var environment = new ImportApprovalEnvironment(server);
        try
        {
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
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        if (pendingMigrations.Length > 0)
            throw new InvalidOperationException("Apply the application's pending migrations before running EndToEnd tests: " +
                string.Join(", ", pendingMigrations) + ". Tests do not migrate the shared database.");
        var branch = new Branch(BranchName);
        var department = new Department(DepartmentName);
        var first = new User($"e2e.first.{runId}", "First reviewer");
        var second = new User($"e2e.second.{runId}", "Second reviewer");
        var role = new Role($"E2E-{runId}");
        string[] permissions = [Permissions.MessageView, Permissions.MessageAssign,
            Permissions.ReviewLevel1, Permissions.ReviewLevel2, Permissions.AuditView];
        foreach (var permission in await db.Permissions.Where(p => permissions.Contains(p.Name)).ToListAsync(ct))
            role.Permissions.Add(new RolePermission { Permission = permission });
        var workflow = new WorkflowDefinition($"E2E_{runId}", "Two independent approvals").AddStep(1, 1).AddStep(2, 2);
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
        // Global rules also apply to new branches. Choose a valid synthetic MX
        // type that none of them match, without changing existing routing.
        var globalRules = await db.DepartmentRoutingRules.AsNoTracking()
            .Where(r => r.IsActive && r.BranchId == null)
            .Select(r => new SaaDepartmentRule
            {
                Id = r.Id, DepartmentId = r.DepartmentId, WorkflowDefinitionId = r.WorkflowDefinitionId,
                Direction = (SaaMessageDirection)r.Direction, MatchKind = (SaaDepartmentMatchKind)r.MatchKind,
                MessageTypePattern = r.MessageTypePattern, IsActive = r.IsActive
            }).ToListAsync(ct);
        for (var attempt = 0; ; attempt++)
        {
            if (attempt == 100) throw new InvalidOperationException("Cannot find an unused E2E message type.");
            var family = new string(Enumerable.Range(0, 4).Select(_ => (char)Random.Shared.Next('a', 'z' + 1)).ToArray());
            MessageType = $"MX{family}.{Random.Shared.Next(1000):D3}.001.01";
            if (SaaDepartmentRuleResolver.SelectRules(globalRules, BranchId, MessageType, SaaMessageDirection.Incoming).Length == 0)
                break;
        }
        db.DepartmentRoutingRules.Add(new DepartmentRoutingRule
        {
            WorkflowDefinitionId = workflow.Id, DepartmentId = department.Id, BranchId = branch.Id, Direction = 1,
            MatchKind = 1, MessageTypePattern = MessageType, IsActive = true,
            SourceFile = $"e2e-{runId}", SourceSheet = "rules", SourceRow = 1
        });
        await db.SaveChangesAsync(ct);

        var catalog = new SaaRoutingCatalog(connection);
        importer = new SaaFileImportService(new SaaSourceFileReader(), new SaaMessageParser(),
            new SaaMessageProcessor(await catalog.LoadDepartmentResolverAsync(), await catalog.LoadBranchResolverAsync()),
            new SqlSaaImportStore(connection));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, FileName),
            $"Branch:{BranchName}|MessageType:{MessageType}|Reference#:{Reference}|DIRECTION:Incoming|SENDER:SENDERXX|RECEIVER:RECEIVERXX|Host:E2E|Time:23/10/26 04:35:39 AM\n" +
            "2026/10/23-04:35:30 PRINT 1\n________\n" + Body, ct);
        factory = new ApiFactory(connection);
    }

    public Task<SaaFileImportResult> ImportAsync(CancellationToken ct) =>
        importer.ImportAsync(Path.Combine(root, FileName), root, cancellationToken: ct);

    public HttpClient Client(string user)
    {
        var client = factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", $"{user}.{runId}");
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
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class ApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureLogging(logging => logging.ClearProviders());
    }
}
