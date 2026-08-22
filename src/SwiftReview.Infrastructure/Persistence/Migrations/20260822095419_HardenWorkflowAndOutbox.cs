using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SwiftReview.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenWorkflowAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId_IsActive",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_LockedUntil_OccurredAt",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "LockId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "OutboxMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.InsertData(
                table: "AuditEvents",
                columns: new[] { "Id", "CorrelationId", "DetailsJson", "EventType", "MessageId", "NewState", "OldState", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 900251L, "seed-0025", "{\"level\":1}", "ReviewApproved", 25L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 9, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900281L, "seed-0028", "{\"level\":1}", "ReviewApproved", 28L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 12, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900311L, "seed-0031", "{\"level\":1}", "ReviewApproved", 31L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 15, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900422L, "seed-0042", "{\"level\":2}", "ReviewApproved", 42L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 24, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 900452L, "seed-0045", "{\"level\":2}", "ReviewApproved", 45L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 24, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 900482L, "seed-0048", "{\"level\":2}", "ReviewApproved", 48L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 24, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 900571L, "seed-0057", "{\"level\":1}", "ReviewApproved", 57L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 17, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900601L, "seed-0060", "{\"level\":1}", "ReviewApproved", 60L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 20, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900631L, "seed-0063", "{\"level\":1}", "ReviewApproved", 63L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 23, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[] { 10, "message.import" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 10, 6 });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "DepartmentId", "BranchId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_LockId",
                table: "OutboxMessages",
                column: "LockId",
                unique: true,
                filter: "[LockId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_NextAttemptAt_LockedUntil_OccurredAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt", "LockedUntil", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_LockId",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_NextAttemptAt_LockedUntil_OccurredAt",
                table: "OutboxMessages");

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900251L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900281L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900311L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900422L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900452L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900482L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900571L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900601L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900631L);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 10, 6 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DropColumn(
                name: "LockId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId_IsActive",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "DepartmentId", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_LockedUntil_OccurredAt",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "LockedUntil", "OccurredAt" });
        }
    }
}
