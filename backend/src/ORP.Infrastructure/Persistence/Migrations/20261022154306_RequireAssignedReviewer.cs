using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireAssignedReviewer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Role composition is data; runtime authorization continues to check permissions.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM orp.Permissions WHERE Name = N'message.assign')
                    INSERT INTO orp.Permissions (Name) VALUES (N'message.assign');
                INSERT INTO orp.RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id FROM orp.Roles r CROSS JOIN orp.Permissions p
                WHERE r.Name = N'Reviewer' AND p.Name = N'message.assign'
                  AND NOT EXISTS (SELECT 1 FROM orp.RolePermissions rp
                                  WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowDefinitions_ConfirmationPolicy",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "AllowSelfAssignment",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ConfirmationActorMode",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowDefinitions_ConfirmationPolicy",
                schema: "orp",
                table: "WorkflowDefinitions",
                sql: "[ConfirmationMode] IN (0, 1, 2) AND [AssignmentRetentionMode] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep role grants: they may have existed before this migration.

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowDefinitions_ConfirmationPolicy",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.AddColumn<bool>(
                name: "AllowSelfAssignment",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationActorMode",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Removed policy values cannot be restored; use the new assignment rule on rollback.
            migrationBuilder.Sql("UPDATE orp.WorkflowDefinitions SET AllowSelfAssignment = 1;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowDefinitions_ConfirmationPolicy",
                schema: "orp",
                table: "WorkflowDefinitions",
                sql: "[ConfirmationMode] IN (0, 1, 2) AND [ConfirmationActorMode] IN (0, 1) AND [AssignmentRetentionMode] IN (0, 1)");
        }
    }
}
