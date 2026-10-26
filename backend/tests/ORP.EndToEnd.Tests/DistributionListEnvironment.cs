using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ORP.Domain.DistributionLists;
using ORP.Domain.Identity;
using ORP.Infrastructure.Persistence;

namespace ORP.EndToEnd.Tests;

internal sealed class DistributionListEnvironment : IAsyncDisposable
{
    private readonly string runId = Guid.NewGuid().ToString("N");
    private readonly List<int> userIds = [];
    private readonly List<int> roleIds = [];
    private readonly List<int> departmentIds = [];
    private readonly List<int> branchIds = [];
    private ApiFactory? factory;
    public DbContextOptions<ORPDbContext> Options { get; private set; } = null!;
    public int DepartmentId => departmentIds[0];
    public int OtherDepartmentId => departmentIds[1];
    public SaveBarrier Barrier { get; } = new();

    public static async Task<DistributionListEnvironment> CreateAsync(CancellationToken ct)
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "EndToEndSettings"))
            .AddJsonFile("appsettings.json").AddJsonFile($"appsettings.{environmentName}.json", optional: true);
        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            builder.AddJsonFile("appsettings.EndToEnd.local.json", optional: true)
                .AddUserSecrets<DistributionListEnvironment>(optional: true);
        using var configuration = (ConfigurationRoot)builder.AddEnvironmentVariables().Build();
        var connection = configuration["ORP_TEST_SQL_SERVER"];
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("ORP");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure ConnectionStrings:ORP or ORP_TEST_SQL_SERVER for distribution list SQL/HTTP tests.");
        var sql = new SqlConnectionStringBuilder(connection);
        if (string.IsNullOrWhiteSpace(sql.InitialCatalog) ||
            new[] { "master", "model", "msdb", "tempdb" }.Contains(sql.InitialCatalog, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Specify an existing application database for distribution list tests.");
        var env = new DistributionListEnvironment
        {
            Options = new DbContextOptionsBuilder<ORPDbContext>().UseSqlServer(connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp")).Options
        };
        try
        {
            await env.PrepareAsync(connection, ct);
            return env;
        }
        catch
        {
            await env.DisposeAsync();
            throw;
        }
    }

    private async Task PrepareAsync(string connection, CancellationToken ct)
    {
        await using var db = new ORPDbContext(Options);
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();
        if (pending.Length > 0)
            throw new InvalidOperationException("Apply pending application migrations before E2E tests: " + string.Join(", ", pending));
        foreach (var suffix in new[] { "first", "second" })
        {
            var department = new Department($"DL-{suffix}-{runId}");
            var branch = new Branch($"DL-{suffix}-{runId}");
            db.AddRange(department, branch);
            await db.SaveChangesAsync(ct);
            departmentIds.Add(department.Id);
            branchIds.Add(branch.Id);
        }
        foreach (var (name, permission) in new[]
                 { ("manager", Permissions.DistributionListManage), ("forwarder", Permissions.MessageForward),
                     ("outsider", Permissions.DistributionListManage), ("viewer", Permissions.MessageView), ("admin", "") })
        {
            var user = new User($"dl.{name}.{runId}", name, name == "admin");
            var role = new Role($"DL-{name}-{runId}");
            if (permission.Length > 0)
                role.Permissions.Add(new RolePermission { Permission = await db.Permissions.SingleAsync(p => p.Name == permission, ct) });
            db.AddRange(user, role);
            await db.SaveChangesAsync(ct);
            userIds.Add(user.Id);
            roleIds.Add(role.Id);
            if (name != "admin")
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id,
                    BranchId = name == "forwarder" ? branchIds[1] : branchIds[0],
                    DepartmentId = name == "outsider" ? OtherDepartmentId : DepartmentId });
            await db.SaveChangesAsync(ct);
        }
        factory = new ApiFactory(connection, Barrier);
    }

    public HttpClient Client(string? name = "manager")
    {
        var client = factory!.CreateClient();
        if (name is not null) client.DefaultRequestHeaders.Add("X-Debug-User", $"dl.{name}.{runId}");
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
            if (departmentIds.Count > 0 || userIds.Count > 0 || roleIds.Count > 0 || branchIds.Count > 0)
            {
                await using var db = new ORPDbContext(Options);
                await using var transaction = await db.Database.BeginTransactionAsync();
                await db.DistributionLists.Where(x => departmentIds.Contains(x.DepartmentId)).ExecuteDeleteAsync();
                await db.UserRoles.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
                await db.RolePermissions.Where(x => roleIds.Contains(x.RoleId)).ExecuteDeleteAsync();
                await db.Roles.Where(x => roleIds.Contains(x.Id)).ExecuteDeleteAsync();
                await db.Users.Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync();
                await db.Departments.Where(x => departmentIds.Contains(x.Id)).ExecuteDeleteAsync();
                await db.Branches.Where(x => branchIds.Contains(x.Id)).ExecuteDeleteAsync();
                await transaction.CommitAsync();
            }
        }
    }

    private sealed class ApiFactory(string connection, SaveBarrier barrier) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .UseSetting("ConnectionStrings:ORP", connection).UseSetting("BootstrapDatabase", "false")
            .ConfigureServices(services => services.AddDbContext<ORPDbContext>(options => options.AddInterceptors(barrier)))
            .ConfigureLogging(logging => logging.ClearProviders());
    }

    internal sealed class SaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int ListId { get; set; }
        public int Arrivals => Volatile.Read(ref arrivals);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (ListId == 0 || !eventData.Context!.ChangeTracker.Entries<DistributionList>()
                    .Any(e => e.Entity.Id == ListId && e.State == EntityState.Modified)) return result;
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
}
