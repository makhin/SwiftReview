using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowReviewerReuse",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentRetentionMode",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationActorMode",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationMode",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RejectAllowed",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "orp",
                table: "Messages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowDefinitions_ConfirmationPolicy",
                schema: "orp",
                table: "WorkflowDefinitions",
                sql: "[ConfirmationMode] IN (0, 1, 2) AND [ConfirmationActorMode] IN (0, 1) AND [AssignmentRetentionMode] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowDefinitions_ConfirmationPolicy",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "AllowReviewerReuse",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "AssignmentRetentionMode",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ConfirmationActorMode",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ConfirmationMode",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "RejectAllowed",
                schema: "orp",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "orp",
                table: "Messages");
        }
    }
}
