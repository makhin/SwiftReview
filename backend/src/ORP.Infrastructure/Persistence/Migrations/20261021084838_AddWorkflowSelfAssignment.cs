using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowSelfAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowSelfAssignment",
                schema: "orp",
                table: "WorkflowDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Preserve the existing profiles' behavior once; runtime uses the stored policy.
            migrationBuilder.Sql("""
                UPDATE [orp].[WorkflowDefinitions]
                SET [AllowSelfAssignment] = 1
                WHERE [Code] IN (N'SINGLE_CONFIRMATION', N'DOUBLE_CONFIRMATION');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowSelfAssignment",
                schema: "orp",
                table: "WorkflowDefinitions");
        }
    }
}
