using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "Direction",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false);

            migrationBuilder.AlterColumn<int>(
                name: "Direction",
                schema: "orp",
                table: "SwiftMessages",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_Direction_DepartmentId_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "Direction", "DepartmentId", "BranchId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowDefinitions_Direction",
                schema: "orp",
                table: "WorkflowDefinitions",
                sql: "[Direction] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SwiftMessages_Direction",
                schema: "orp",
                table: "SwiftMessages",
                sql: "[Direction] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_MessageType_Direction_DepartmentId_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowDefinitions_Direction",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SwiftMessages_Direction",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "Direction",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.AlterColumn<string>(
                name: "Direction",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "DepartmentId", "BranchId" },
                unique: true,
                filter: "[IsActive] = 1");
        }
    }
}
