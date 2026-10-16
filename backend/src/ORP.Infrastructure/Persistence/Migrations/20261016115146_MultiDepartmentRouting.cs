using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiDepartmentRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Departments_DepartmentId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_Departments_DepartmentId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_DepartmentId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_MessageType_Direction_DepartmentId_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Messages_DepartmentId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_DepartmentRoutingRules_Direction_BranchId_Priority",
                schema: "orp",
                table: "DepartmentRoutingRules");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "orp",
                table: "DepartmentRoutingRules");

            migrationBuilder.CreateTable(
                name: "MessageDepartments",
                schema: "orp",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageDepartments", x => new { x.MessageId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_MessageDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageDepartments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "orp",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_WorkflowDefinitions_MessageType_Direction_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "Direction", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRoutingRules_Direction_BranchId",
                schema: "orp",
                table: "DepartmentRoutingRules",
                columns: new[] { "Direction", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDepartments_DepartmentId",
                schema: "orp",
                table: "MessageDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDepartments_DepartmentId",
                schema: "orp",
                table: "WorkflowDepartments",
                column: "DepartmentId");

            migrationBuilder.Sql("INSERT INTO [orp].[WorkflowDepartments] ([WorkflowDefinitionId], [DepartmentId]) SELECT [Id], [DepartmentId] FROM [orp].[WorkflowDefinitions]");
            migrationBuilder.Sql("INSERT INTO [orp].[MessageDepartments] ([MessageId], [DepartmentId]) SELECT [MessageId], [DepartmentId] FROM [orp].[Messages] WHERE [DepartmentId] IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "orp",
                table: "Messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_MessageType_Direction_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DepartmentRoutingRules_Direction_BranchId",
                schema: "orp",
                table: "DepartmentRoutingRules");

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                schema: "orp",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                schema: "orp",
                table: "DepartmentRoutingRules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE [orp].[WorkflowDefinitions] SET [DepartmentId] = (SELECT MIN(d.[DepartmentId]) FROM [orp].[WorkflowDepartments] d WHERE d.[WorkflowDefinitionId] = [orp].[WorkflowDefinitions].[Id])");
            migrationBuilder.Sql("UPDATE [orp].[Messages] SET [DepartmentId] = (SELECT MIN(d.[DepartmentId]) FROM [orp].[MessageDepartments] d WHERE d.[MessageId] = [orp].[Messages].[MessageId])");

            migrationBuilder.DropTable(name: "MessageDepartments", schema: "orp");
            migrationBuilder.DropTable(name: "WorkflowDepartments", schema: "orp");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_DepartmentId",
                schema: "orp",
                table: "WorkflowDefinitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_Direction_DepartmentId_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "Direction", "DepartmentId", "BranchId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DepartmentId",
                schema: "orp",
                table: "Messages",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRoutingRules_Direction_BranchId_Priority",
                schema: "orp",
                table: "DepartmentRoutingRules",
                columns: new[] { "Direction", "BranchId", "Priority" });

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Departments_DepartmentId",
                schema: "orp",
                table: "Messages",
                column: "DepartmentId",
                principalSchema: "orp",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_Departments_DepartmentId",
                schema: "orp",
                table: "WorkflowDefinitions",
                column: "DepartmentId",
                principalSchema: "orp",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
