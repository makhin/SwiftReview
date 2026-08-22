using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SwiftReview.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixThreeLevelWorkflowSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AuditEvents",
                columns: new[] { "Id", "CorrelationId", "DetailsJson", "EventType", "MessageId", "NewState", "OldState", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 5903L, "seed-0059", "{\"level\":1}", "ReviewStarted", 59L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5904L, "seed-0059", "{\"level\":1}", "ReviewApproved", 59L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5905L, "seed-0059", "{\"level\":2}", "ReviewStarted", 59L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5906L, "seed-0059", "{\"level\":2}", "ReviewApproved", 59L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5907L, "seed-0059", "{\"level\":3}", "ReviewStarted", 59L, "ThirdReviewInProgress", "WaitingForThirdReview", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 5908L, "seed-0059", "{\"level\":3}", "MessageCompleted", 59L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 6203L, "seed-0062", "{\"level\":1}", "ReviewStarted", 62L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6204L, "seed-0062", "{\"level\":1}", "ReviewApproved", 62L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6205L, "seed-0062", "{\"level\":2}", "ReviewStarted", 62L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6206L, "seed-0062", "{\"level\":2}", "ReviewApproved", 62L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6207L, "seed-0062", "{\"level\":3}", "ReviewStarted", 62L, "ThirdReviewInProgress", "WaitingForThirdReview", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 6208L, "seed-0062", "{\"level\":3}", "MessageCompleted", 62L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 900593L, "seed-0059", "{\"level\":3}", "ReviewApproved", 59L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 34, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 900623L, "seed-0062", "{\"level\":3}", "ReviewApproved", 62L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 34, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 }
                });

            migrationBuilder.InsertData(
                table: "Reviews",
                columns: new[] { "Id", "Comment", "CompletedAt", "Level", "MessageId", "ReviewerId", "StartedAt", "Status" },
                values: new object[,]
                {
                    { 591L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 59L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 592L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 59L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 593L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, 59L, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 621L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 62L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 622L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 62L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 623L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, 62L, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" }
                });

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 4,
                column: "WorkflowDefinitionId",
                value: 3);

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Order", "ReviewLevel", "WorkflowDefinitionId" },
                values: new object[] { 2, 2, 3 });

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Order", "ReviewLevel", "WorkflowDefinitionId" },
                values: new object[] { 3, 3, 3 });

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 7,
                column: "WorkflowDefinitionId",
                value: 4);

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 8,
                column: "WorkflowDefinitionId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 9,
                column: "WorkflowDefinitionId",
                value: 5);

            migrationBuilder.InsertData(
                table: "WorkflowSteps",
                columns: new[] { "Id", "Order", "Required", "ReviewLevel", "WorkflowDefinitionId" },
                values: new object[,]
                {
                    { 10, 1, true, 1, 6 },
                    { 11, 2, true, 2, 6 },
                    { 12, 3, true, 3, 6 },
                    { 13, 1, true, 1, 7 },
                    { 14, 1, true, 1, 8 },
                    { 15, 2, true, 2, 8 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 5903L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 5904L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 5905L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 5906L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 5907L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 5908L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 6203L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 6204L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 6205L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 6206L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 6207L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 6208L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900593L);

            migrationBuilder.DeleteData(
                table: "AuditEvents",
                keyColumn: "Id",
                keyValue: 900623L);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 591L);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 592L);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 593L);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 621L);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 622L);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 623L);

            migrationBuilder.DeleteData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 4,
                column: "WorkflowDefinitionId",
                value: 4);

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Order", "ReviewLevel", "WorkflowDefinitionId" },
                values: new object[] { 1, 1, 5 });

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Order", "ReviewLevel", "WorkflowDefinitionId" },
                values: new object[] { 2, 2, 5 });

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 7,
                column: "WorkflowDefinitionId",
                value: 7);

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 8,
                column: "WorkflowDefinitionId",
                value: 8);

            migrationBuilder.UpdateData(
                table: "WorkflowSteps",
                keyColumn: "Id",
                keyValue: 9,
                column: "WorkflowDefinitionId",
                value: 8);
        }
    }
}
