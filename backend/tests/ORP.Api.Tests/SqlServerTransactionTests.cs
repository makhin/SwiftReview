using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ORP.Application;
using ORP.Application.Abstractions;
using ORP.Application.Administration;
using ORP.Application.Assignments.Assign;
using ORP.Application.Messages.ChangeWorkflow;
using ORP.Application.Reviews;
using ORP.Domain.Auditing;
using ORP.Domain.Messages;
using ORP.Infrastructure;
using ORP.Infrastructure.Identity;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Api.Tests;

public sealed class SqlServerTransactionTests
{
    public static bool SqlServerConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORP_TEST_SQL_SERVER"));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Skip = "Set ORP_TEST_SQL_SERVER to a SQL Server connection with database creation permission.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task DirectHandlers_AuthorizeAndMutateInsideTransaction_WithoutHttpOrAdminClaims()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ORPDbContext>();
        var message = await db.Messages.AsNoTracking().OrderBy(m => m.Id).FirstAsync(Ct);
        fixture.Current.UserId = await db.Users.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync(Ct);
        // The database grants administrative access; the transport snapshot deliberately does not.
        fixture.Current.IsGlobalAdministrator = false;
        await services.GetRequiredService<ChangeMessageWorkflowHandler>().HandleAsync(message.Id,
            new ChangeMessageWorkflowRequest(message.WorkflowDefinitionId), Ct);
        var reviewer = await db.Users.Where(u => u.UserName == "amelia.hart").Select(u => u.Id).SingleAsync(Ct);
        await services.GetRequiredService<AssignMessageHandler>().HandleAsync(message.Id, new AssignMessageRequest(reviewer), Ct);
        fixture.Current.UserId = reviewer;
        var reviewId = await services.GetRequiredService<StartReviewHandler>().HandleAsync(message.Id, new StartReviewRequest(1), Ct);
        await services.GetRequiredService<CancelReviewHandler>().HandleAsync(message.Id, new CancelReviewRequest(1, reviewId), Ct);
        reviewId = await services.GetRequiredService<StartReviewHandler>().HandleAsync(message.Id, new StartReviewRequest(1), Ct);
        await services.GetRequiredService<ApproveReviewHandler>().HandleAsync(message.Id, new ApproveReviewRequest(1, reviewId, null), Ct);

        // Stale administrative claims must not authorize a direct call.
        fixture.Current.IsGlobalAdministrator = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => services.GetRequiredService<UndoReviewHandler>()
            .HandleAsync(message.Id, new UndoReviewRequest(reviewId, null), Ct));
        fixture.Current.UserId = await db.Users.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync(Ct);
        fixture.Current.IsGlobalAdministrator = false;
        await services.GetRequiredService<UndoReviewHandler>().HandleAsync(message.Id, new UndoReviewRequest(reviewId, null), Ct);
        Assert.True(services.GetRequiredService<CheckedAccess>().Reads >= 7);
        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact(Skip = "Set ORP_TEST_SQL_SERVER to a SQL Server connection with database creation permission.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task DirectHandler_FailureAfterSave_RollsBackMessageReviewAndAudit()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var messageId = await AssignFirstMessageAsync(fixture, services);
        var db = services.GetRequiredService<ORPDbContext>();
        var auditCount = await db.AuditEvents.CountAsync(Ct);
        fixture.Failure.NextFailure = new InvalidOperationException("Injected failure after SaveChanges");
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<StartReviewHandler>()
            .HandleAsync(messageId, new StartReviewRequest(1), Ct));

        Assert.Null(db.Database.CurrentTransaction);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(MessageState.Assigned, await db.Messages.Where(m => m.Id == messageId).Select(m => m.State).SingleAsync(Ct));
        Assert.False(await db.Reviews.AnyAsync(r => r.MessageId == messageId, Ct));
        Assert.Equal(auditCount, await db.AuditEvents.CountAsync(Ct));
    }

    [Fact(Skip = "Set ORP_TEST_SQL_SERVER to a SQL Server connection with database creation permission.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task DirectHandler_TransientFailure_ReloadsAccessAndPersistsOnlyOneReview()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var messageId = await AssignFirstMessageAsync(fixture, services);
        var access = services.GetRequiredService<CheckedAccess>();
        var previousReads = access.Reads;
        fixture.Failure.NextFailure = new TimeoutException("Injected transient failure after SaveChanges");
        var reviewId = await services.GetRequiredService<StartReviewHandler>().HandleAsync(messageId, new StartReviewRequest(1), Ct);

        Assert.Equal(previousReads + 2, access.Reads);
        var db = services.GetRequiredService<ORPDbContext>();
        Assert.Equal(reviewId, await db.Reviews.Where(r => r.MessageId == messageId).Select(r => r.Id).SingleAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.MessageId == messageId && a.EventType == AuditEventType.ReviewStarted, Ct));
        Assert.Equal(MessageState.FirstReviewInProgress, await db.Messages.Where(m => m.Id == messageId).Select(m => m.State).SingleAsync(Ct));
    }

    [Fact(Skip = "Set ORP_TEST_SQL_SERVER to a SQL Server connection with database creation permission.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task DirectHandler_RetryRejectsPermissionsRevokedBetweenAttempts()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var messageId = await AssignFirstMessageAsync(fixture, services);
        var db = services.GetRequiredService<ORPDbContext>();
        var reviewer = fixture.Current.UserId;
        var access = services.GetRequiredService<CheckedAccess>();
        var previousReads = access.Reads;
        fixture.Current.IsGlobalAdministrator = true;
        fixture.Failure.NextFailure = new TimeoutException("Injected transient failure");
        fixture.Failure.OnFailure = () => fixture.BeforeTransaction.Once = async ct =>
        {
            // This separate writer runs after the failed attempt rolls back, before the next begins.
            await using var connection = new SqlConnection(db.Database.GetConnectionString());
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM orp.UserRoles WHERE UserId = @userId";
            command.Parameters.AddWithValue("@userId", reviewer);
            await command.ExecuteNonQueryAsync(ct);
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => services.GetRequiredService<StartReviewHandler>()
            .HandleAsync(messageId, new StartReviewRequest(1), Ct));
        Assert.Equal(previousReads + 2, access.Reads);
        Assert.False(await db.UserRoles.AnyAsync(r => r.UserId == reviewer, Ct));
        Assert.False(await db.Reviews.AnyAsync(r => r.MessageId == messageId, Ct));
        Assert.False(await db.AuditEvents.AnyAsync(a => a.MessageId == messageId && a.EventType == AuditEventType.ReviewStarted, Ct));
        Assert.Equal(MessageState.Assigned, await db.Messages.Where(m => m.Id == messageId).Select(m => m.State).SingleAsync(Ct));
    }

    [Fact(Skip = "Set ORP_TEST_SQL_SERVER to a SQL Server connection with database creation permission.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task Administration_UsesSameTransactionExecutor_AndRollsBackAccessAndAudit()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ORPDbContext>();
        fixture.Current.UserId = await db.Users.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync(Ct);
        var reviewer = await db.Users.Where(u => u.UserName == "amelia.hart").Select(u => u.Id).SingleAsync(Ct);
        var oldAssignments = await db.UserRoles.CountAsync(r => r.UserId == reviewer, Ct);
        var auditCount = await db.AccessAuditEvents.CountAsync(Ct);
        var service = services.GetRequiredService<IUserAdministrationService>();
        fixture.Failure.NextFailure = new InvalidOperationException("Injected failure after access changes");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUserAsync(reviewer, new UpdateUserAccessRequest([]), Ct));
        Assert.Equal(oldAssignments, await db.UserRoles.CountAsync(r => r.UserId == reviewer, Ct));
        Assert.Equal(auditCount, await db.AccessAuditEvents.CountAsync(Ct));
        await service.UpdateUserAsync(reviewer, new UpdateUserAccessRequest([]), Ct);
        Assert.False(await db.UserRoles.AnyAsync(r => r.UserId == reviewer, Ct));
        Assert.Equal(auditCount + 1, await db.AccessAuditEvents.CountAsync(Ct));
        fixture.Current.UserId = reviewer;
        fixture.Current.IsGlobalAdministrator = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateUserAsync(reviewer, new UpdateUserAccessRequest([]), Ct));
    }

    [Fact(Skip = "Set ORP_TEST_SQL_SERVER to a SQL Server connection with database creation permission.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task AuthorizationQueries_UseOneStatement_AndPreserveScopedPermissions()
    {
        await using var fixture = await SqlFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetRequiredService<ORPDbContext>().Database.GetConnectionString();
        var commands = new ReadCommands();
        await using var db = new ORPDbContext(new DbContextOptionsBuilder<ORPDbContext>()
            .UseSqlServer(connection).AddInterceptors(commands).Options);
        var queries = new UserAuthorizationQueries(db);
        var identity = await queries.GetIdentityAsync("amelia.hart", Ct);
        Assert.NotNull(identity);
        Assert.DoesNotContain("UserRoles", Assert.Single(commands.Statements));
        var access = await new UserAccessService(db).GetByIdAsync(identity.UserId, Ct);
        Assert.NotNull(access);
        var pairs = access.Scopes.Select(s => (s.BranchId, s.DepartmentId)).Append((int.MaxValue, int.MaxValue));
        foreach (var (branch, department) in pairs)
        foreach (var permission in new[] { ORP.Domain.Identity.Permissions.ReviewLevel1, ORP.Domain.Identity.Permissions.WorkflowManage })
        {
            commands.Statements.Clear();
            var check = await queries.CheckAsync(identity.UserId, branch, department, permission, Ct);
            Assert.NotNull(check);
            Assert.Single(commands.Statements);
            Assert.Equal(access.IsGlobalAdministrator, check.IsGlobalAdministrator);
            Assert.Equal(access.CanAccess(branch, department), check.CanView);
            Assert.Equal(access.HasPermission(permission, branch, department), check.HasPermission);
        }
        commands.Statements.Clear();
        Assert.Null(await queries.CheckAsync(int.MaxValue, 1, 1, "message.view", Ct));
        Assert.Single(commands.Statements);
        var visibleWorkflows = await new ReferenceDataQueries(db).GetWorkflowsAsync(access, Ct);
        var workflows = await db.WorkflowDefinitions.Select(w => w.Id).ToListAsync(Ct);
        foreach (var id in workflows)
        {
            commands.Statements.Clear();
            Assert.Equal(visibleWorkflows.Any(w => w.Id == id), await queries.CanAccessWorkflowAsync(identity.UserId, id, Ct));
            Assert.Single(commands.Statements);
        }
    }

    private sealed class ReadCommands : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private static async Task<long> AssignFirstMessageAsync(SqlFixture fixture, IServiceProvider services)
    {
        var db = services.GetRequiredService<ORPDbContext>();
        var id = await db.Messages.OrderBy(m => m.Id).Select(m => m.Id).FirstAsync(Ct);
        fixture.Current.UserId = await db.Users.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync(Ct);
        var reviewer = await db.Users.Where(u => u.UserName == "amelia.hart").Select(u => u.Id).SingleAsync(Ct);
        await services.GetRequiredService<AssignMessageHandler>().HandleAsync(id, new AssignMessageRequest(reviewer), Ct);
        fixture.Current.UserId = reviewer;
        return id;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public int UserId { get; set; }
        public string UserName => "transaction-test";
        public string DisplayName => UserName;
        public bool IsGlobalAdministrator { get; set; }
    }

    private sealed class TestCorrelation : ICorrelationContext
    {
        public string CorrelationId => "transaction-test";
    }

    private sealed class CheckedAccess(ORPDbContext db) : IUserAuthorizationQueries
    {
        private readonly UserAuthorizationQueries inner = new(db);
        public int Reads { get; private set; }
        public Task<UserPermissionCheck?> CheckAsync(int userId, int branchId, int departmentId, string permission, CancellationToken ct)
        {
            Assert.Equal(IsolationLevel.Serializable, db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel);
            Reads++;
            return inner.CheckAsync(userId, branchId, departmentId, permission, ct);
        }
        public Task<UserIdentity?> GetIdentityAsync(int userId, CancellationToken ct) => inner.GetIdentityAsync(userId, ct);
        public Task<UserIdentity?> GetIdentityAsync(string userName, CancellationToken ct) => inner.GetIdentityAsync(userName, ct);
        public Task<bool> CanAccessWorkflowAsync(int userId, int workflowId, CancellationToken ct) => inner.CanAccessWorkflowAsync(userId, workflowId, ct);
    }

    private sealed class FailureAfterSave : SaveChangesInterceptor
    {
        public Exception? NextFailure { get; set; }
        public Action? OnFailure { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(IsolationLevel.Serializable, eventData.Context!.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel);
            if (NextFailure is { } failure)
            {
                NextFailure = null;
                OnFailure?.Invoke();
                throw failure;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BeforeTransaction : DbTransactionInterceptor
    {
        public Func<CancellationToken, Task>? Once { get; set; }
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            if (Once is { } action)
            {
                Once = null;
                await action(cancellationToken);
            }
            return result;
        }
    }

    private sealed class SqlFixture(ServiceProvider services, TestCurrentUser current, FailureAfterSave failure, BeforeTransaction beforeTransaction) : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;
        public TestCurrentUser Current { get; } = current;
        public FailureAfterSave Failure { get; } = failure;
        public BeforeTransaction BeforeTransaction { get; } = beforeTransaction;

        public static async Task<SqlFixture> CreateAsync()
        {
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ORP_TEST_SQL_SERVER"))
            {
                InitialCatalog = $"ORP_TransactionTests_{Guid.NewGuid():N}"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ORP"] = connection.ConnectionString
            }).Build();
            var current = new TestCurrentUser();
            var failure = new FailureAfterSave();
            var beforeTransaction = new BeforeTransaction();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ICurrentUser>(current);
            services.AddSingleton<ICorrelationContext, TestCorrelation>();
            services.AddApplication();
            services.AddInfrastructure(configuration);
            services.AddScoped<CheckedAccess>();
            services.AddScoped<IUserAuthorizationQueries>(sp => sp.GetRequiredService<CheckedAccess>());
            services.AddDbContext<ORPDbContext>(options => options.AddInterceptors(failure, beforeTransaction));
            var fixture = new SqlFixture(services.BuildServiceProvider(), current, failure, beforeTransaction);
            try
            {
                await using var scope = fixture.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ORPDbContext>();
                await db.Database.MigrateAsync(Ct);
                var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "seed-test-data.sql"), Ct);
                await db.Database.OpenConnectionAsync(Ct);
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(Ct);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var scope = Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ORPDbContext>().Database.EnsureDeletedAsync(CancellationToken.None);
            }
            finally { await Services.DisposeAsync(); }
        }
    }
}
