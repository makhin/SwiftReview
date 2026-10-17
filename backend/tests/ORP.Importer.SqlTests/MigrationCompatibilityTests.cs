using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ORP.Infrastructure.Persistence;
using Xunit;

namespace ORP.Importer.SqlTests;

public sealed class MigrationCompatibilityTests
{
    [Fact]
    public async Task MainSchemaUpgradesAndAlreadyMigratedSchemaIsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await SqlImportDatabase.CreateAsync("20260930162524_AddMessageDirection");
        await using var db = new ORPDbContext(fixture.Options);
        // This database is disposable. Preserve the existing SAA migration's explicit
        // reset of warehouse messages and verify that reference data survives it.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO orp.Branches (Name) VALUES (N'Legacy branch');
            INSERT INTO orp.Departments (Name) VALUES (N'CS');
            INSERT INTO orp.Users (UserName, DisplayName, IsGlobalAdministrator) VALUES (N'legacy', N'Legacy user', 1);
            INSERT INTO orp.WorkflowDefinitions (Name, MessageType, DepartmentId, IsActive, Direction,
                UndoApprovalMode, UndoActorMode, UndoActiveReviewMode)
            VALUES (N'Legacy workflow', N'MT199', 1, 1, 1, 0, 0, 0);
            INSERT INTO orp.WorkflowSteps (WorkflowDefinitionId, [Order], ReviewLevel, Required) VALUES (1, 1, 1, 1);
            INSERT INTO orp.SwiftMessages (WarehouseId, BodyContainsMx, BodyContainsMt, MessageLength,
                MessageType, RoutingStatus, LastSynchronizedAtUtc, BranchId, DepartmentId)
            VALUES (N'legacy', 0, 1, 5, N'MT199', N'Ready', SYSUTCDATETIME(), 1, 1);
            INSERT INTO orp.Messages (MessageId, State, WorkflowDefinitionId) VALUES (1, N'New', 1);
            """, ct);
        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: ct);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
        Assert.Single(await db.Users.ToListAsync(ct));
        Assert.Single(await db.Branches.ToListAsync(ct));
        Assert.Equal("LEGACY_1", (await db.WorkflowDefinitions.SingleAsync(ct)).Code);
        Assert.Empty(await db.Messages.ToListAsync(ct));
        Assert.Empty(await db.SwiftMessages.ToListAsync(ct));
        var migrations = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        Assert.Equal(9, migrations.Length);
        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: ct);
        Assert.Equal(migrations, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.Single(await db.Users.ToListAsync(ct));
    }
}
