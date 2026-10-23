using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations;

public sealed partial class AddBulkApprovePermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF NOT EXISTS (SELECT 1 FROM orp.Permissions WHERE Name = N'review.bulk-approve')
            INSERT INTO orp.Permissions (Name) VALUES (N'review.bulk-approve');
        INSERT INTO orp.RolePermissions (RoleId, PermissionId)
        SELECT r.Id, p.Id FROM orp.Roles r CROSS JOIN orp.Permissions p
        WHERE r.Name = N'Reviewer' AND p.Name = N'review.bulk-approve'
          AND NOT EXISTS (SELECT 1 FROM orp.RolePermissions rp
                          WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE rp FROM orp.RolePermissions rp
        INNER JOIN orp.Permissions p ON p.Id = rp.PermissionId WHERE p.Name = N'review.bulk-approve';
        DELETE FROM orp.Permissions WHERE Name = N'review.bulk-approve';
        """);
}
