using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReusableWorkflowRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_Branches_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "WorkflowDepartments",
                schema: "orp");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_MessageType_Direction_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowDefinitions_Direction",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "Direction",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "MessageType",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CanProcess",
                schema: "orp",
                table: "MessageDepartments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowDefinitionId",
                schema: "orp",
                table: "DepartmentRoutingRules",
                type: "int",
                nullable: true);

            // Preserve IDs and history without pretending old routing has been migrated.
            migrationBuilder.Sql("UPDATE orp.WorkflowDefinitions SET Code = CONCAT('LEGACY_', Id)");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_Code",
                schema: "orp",
                table: "WorkflowDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRoutingRules_WorkflowDefinitionId",
                schema: "orp",
                table: "DepartmentRoutingRules",
                column: "WorkflowDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DepartmentRoutingRules_WorkflowDefinitions_WorkflowDefinitionId",
                schema: "orp",
                table: "DepartmentRoutingRules",
                column: "WorkflowDefinitionId",
                principalSchema: "orp",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepartmentRoutingRules_WorkflowDefinitions_WorkflowDefinitionId",
                schema: "orp",
                table: "DepartmentRoutingRules");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_Code",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DepartmentRoutingRules_WorkflowDefinitionId",
                schema: "orp",
                table: "DepartmentRoutingRules");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "CanProcess",
                schema: "orp",
                table: "MessageDepartments");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionId",
                schema: "orp",
                table: "DepartmentRoutingRules");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Direction",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "WorkflowDepartments",
                schema: "orp",
                columns: table => new
                {
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDepartments", x => new { x.WorkflowDefinitionId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_WorkflowDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDepartments_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "orp",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_Direction_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "Direction", "BranchId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowDefinitions_Direction",
                schema: "orp",
                table: "WorkflowDefinitions",
                sql: "[Direction] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDepartments_DepartmentId",
                schema: "orp",
                table: "WorkflowDepartments",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_Branches_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                column: "BranchId",
                principalSchema: "orp",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
