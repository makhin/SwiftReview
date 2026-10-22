using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentsByLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_MessageId",
                schema: "orp",
                table: "Assignments");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                schema: "orp",
                table: "Assignments",
                type: "int",
                nullable: true);

            // Only active assignments have a reliably known level. Keep old history unknown.
            migrationBuilder.Sql("""
                UPDATE a SET [Level] = CASE
                    WHEN m.State IN (N'New', N'Assigned', N'FirstReviewInProgress') THEN 1
                    WHEN m.State IN (N'WaitingForSecondReview', N'SecondReviewInProgress') THEN 2
                    WHEN m.State IN (N'WaitingForThirdReview', N'ThirdReviewInProgress') THEN 3
                    END,
                    EndedAt = CASE WHEN m.State IN (N'Completed', N'Rejected', N'ViewOnly')
                                   THEN SYSDATETIMEOFFSET() ELSE a.EndedAt END
                FROM orp.Assignments a JOIN orp.Messages m ON m.MessageId = a.MessageId
                WHERE a.EndedAt IS NULL;
                UPDATE orp.Messages SET CurrentAssigneeId = NULL
                WHERE State IN (N'Completed', N'Rejected', N'ViewOnly') AND CurrentAssigneeId IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MessageId_Level",
                schema: "orp",
                table: "Assignments",
                columns: new[] { "MessageId", "Level" },
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assignments_Level",
                schema: "orp",
                table: "Assignments",
                sql: "([Level] BETWEEN 1 AND 3) AND ([EndedAt] IS NOT NULL OR [Level] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback cannot represent multiple open levels. Refuse rather than discard assignments.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT MessageId FROM orp.Assignments WHERE EndedAt IS NULL
                           GROUP BY MessageId HAVING COUNT(*) > 1)
                    THROW 51000, 'Close multiple level assignments before rolling back AssignmentsByLevel.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Assignments_MessageId_Level",
                schema: "orp",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assignments_Level",
                schema: "orp",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "orp",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MessageId",
                schema: "orp",
                table: "Assignments",
                column: "MessageId",
                unique: true,
                filter: "[EndedAt] IS NULL");
        }
    }
}
