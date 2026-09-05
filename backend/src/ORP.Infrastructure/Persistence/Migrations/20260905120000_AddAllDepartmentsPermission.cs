using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ORPDbContext))]
[Migration("20260905120000_AddAllDepartmentsPermission")]
public sealed class AddAllDepartmentsPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM [ORP].[Permissions]
                WHERE [Name] = N'message.access.all-departments'
            )
            BEGIN
                INSERT INTO [ORP].[Permissions] ([Name])
                VALUES (N'message.access.all-departments');
            END;

            INSERT INTO [ORP].[RolePermissions] ([RoleId], [PermissionId])
            SELECT role.[Id], permission.[Id]
            FROM [ORP].[Roles] AS role
            CROSS JOIN [ORP].[Permissions] AS permission
            WHERE role.[Name] = N'Administrator'
              AND permission.[Name] = N'message.access.all-departments'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [ORP].[RolePermissions] AS existing
                  WHERE existing.[RoleId] = role.[Id]
                    AND existing.[PermissionId] = permission.[Id]
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE rolePermission
            FROM [ORP].[RolePermissions] AS rolePermission
            INNER JOIN [ORP].[Permissions] AS permission
                ON permission.[Id] = rolePermission.[PermissionId]
            WHERE permission.[Name] = N'message.access.all-departments';

            DELETE FROM [ORP].[Permissions]
            WHERE [Name] = N'message.access.all-departments';
            """);
    }
}
