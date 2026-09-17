using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class MessageApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqlTestDatabase database = new();
    private readonly IInterceptor[] interceptors;

    private Dictionary<string, int> users = [];
    private Dictionary<string, int> roles = [];
    private Dictionary<string, int> branches = [];
    private Dictionary<string, int> departments = [];
    private Dictionary<string, int> workflows = [];
    private Dictionary<string, long> messages = [];


    public MessageApiFactory() : this([]) { }
    internal MessageApiFactory(params IInterceptor[] interceptors) => this.interceptors = interceptors;

    public int UserId(string name) => users[name];
    public int RoleId(string name) => roles[name];
    public int BranchId(string name) => branches[name];
    public int DepartmentId(string name) => departments[name];
    public int WorkflowId(string messageType) => workflows[messageType];
    public long MessageId(int seedNumber) => messages[$"TEST-{seedNumber:00000}"];

    public async ValueTask InitializeAsync()
    {
        try
        {
            var ct = TestContext.Current.CancellationToken;
            await database.InitializeAsync(ct);
            await using var db = database.CreateContext();
            users = await db.Users.ToDictionaryAsync(x => x.UserName, x => x.Id, ct);
            roles = await db.Roles.ToDictionaryAsync(x => x.Name, x => x.Id, ct);
            branches = await db.Branches.ToDictionaryAsync(x => x.Name, x => x.Id, ct);
            departments = await db.Departments.ToDictionaryAsync(x => x.Name, x => x.Id, ct);
            workflows = await db.WorkflowDefinitions.ToDictionaryAsync(x => x.MessageType, x => x.Id, ct);
            messages = await db.SwiftMessages.ToDictionaryAsync(x => x.WarehouseId, x => x.MessageId, ct);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(Settings));
        builder.ConfigureTestServices(services =>
        {
            // Never let developer environment variables or user secrets select the test database.
            services.RemoveAll<ORPDbContext>();
            services.RemoveAll<DbContextOptions<ORPDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ORPDbContext>>();
            services.AddDbContext<ORPDbContext>(options => options.UseSqlServer(database.ConnectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "orp");
                sql.EnableRetryOnFailure();
            }).AddInterceptors(interceptors));
        });
    }

    private Dictionary<string, string?> Settings => new()
    {
        ["ConnectionStrings:ORP"] = database.ConnectionString,
        ["BootstrapDatabase"] = "false"
    };

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Host configuration is available before Program registers Infrastructure.
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(Settings));
        return base.CreateHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { await database.DisposeAsync(); }
    }
}
