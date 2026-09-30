using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowUndoPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UndoActiveReviewMode",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UndoActorMode",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UndoApprovalMode",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowDefinitions_UndoPolicy",
                schema: "orp",
                table: "WorkflowDefinitions",
                sql: "[UndoApprovalMode] IN (0, 1, 2) AND [UndoActorMode] IN (0, 1) AND [UndoActiveReviewMode] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowDefinitions_UndoPolicy",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "UndoActiveReviewMode",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "UndoActorMode",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "UndoApprovalMode",
                schema: "orp",
                table: "WorkflowDefinitions");
        }
    }
}
