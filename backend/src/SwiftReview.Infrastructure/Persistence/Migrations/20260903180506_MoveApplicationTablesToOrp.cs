using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SwiftReview.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveApplicationTablesToOrp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Branches_BranchId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Departments_OwningDepartmentId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "MessageRawData");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_BranchId_OwningDepartmentId_ReceivedAt_Id",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_BranchId_OwningDepartmentId_State_ReceivedAt_Id",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ExternalId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_OwningDepartmentId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Account",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "OwningDepartmentId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Receiver",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Sender",
                table: "Messages");

            migrationBuilder.EnsureSchema(
                name: "ORP");

            migrationBuilder.Sql(
                """
                ALTER SCHEMA [ORP] TRANSFER [dbo].[WorkflowSteps];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[WorkflowDefinitions];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Users];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[UserRoles];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[UserDepartments];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[UserBranches];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Roles];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[RolePermissions];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Reviews];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Permissions];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Messages];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Departments];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Branches];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[AuditEvents];
                ALTER SCHEMA [ORP] TRANSFER [dbo].[Assignments];
                EXEC sp_rename N'[ORP].[Messages].[Id]', N'MessageId', N'COLUMN';
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[ORP].[SwiftMessageSource]') IS NULL
                BEGIN
                    EXEC(N'
                        CREATE VIEW [ORP].[SwiftMessageSource]
                        AS
                        SELECT
                            CAST(NULL AS bigint) AS [MessageID],
                            CAST(NULL AS nvarchar(100)) AS [ExternalId],
                            CAST(NULL AS nvarchar(20)) AS [MessageType],
                            CAST(NULL AS int) AS [BranchId],
                            CAST(NULL AS int) AS [DepartmentId],
                            CAST(NULL AS datetimeoffset) AS [ReceivedAt],
                            CAST(NULL AS nvarchar(100)) AS [Sender],
                            CAST(NULL AS nvarchar(100)) AS [Receiver],
                            CAST(NULL AS nvarchar(100)) AS [Account],
                            CAST(NULL AS nvarchar(3)) AS [Currency],
                            CAST(NULL AS decimal(19, 4)) AS [Amount],
                            CAST(NULL AS nvarchar(200)) AS [Reference]
                        WHERE 1 = 0;');
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE [ORP].[RegisterNewMessages]
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SET IDENTITY_INSERT [ORP].[Messages] ON;

                    INSERT INTO [ORP].[Messages]
                        ([MessageId], [State], [CurrentAssigneeId], [WorkflowDefinitionId])
                    SELECT
                        source.[MessageID],
                        N'New',
                        NULL,
                        workflow.[Id]
                    FROM [ORP].[SwiftMessageSource] AS source
                    CROSS APPLY
                    (
                        SELECT TOP (1) candidate.[Id]
                        FROM [ORP].[WorkflowDefinitions] AS candidate
                        WHERE candidate.[IsActive] = 1
                          AND candidate.[MessageType] = source.[MessageType]
                          AND candidate.[DepartmentId] = source.[DepartmentId]
                          AND (candidate.[BranchId] = source.[BranchId] OR candidate.[BranchId] IS NULL)
                        ORDER BY
                            CASE WHEN candidate.[BranchId] = source.[BranchId] THEN 0 ELSE 1 END,
                            candidate.[Id]
                    ) AS workflow
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM [ORP].[Messages] AS existing
                        WHERE existing.[MessageId] = source.[MessageID]
                    );

                    SET IDENTITY_INSERT [ORP].[Messages] OFF;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [ORP].[RegisterNewMessages];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [ORP].[SwiftMessageSource];");

            migrationBuilder.Sql(
                """
                EXEC sp_rename N'[ORP].[Messages].[MessageId]', N'Id', N'COLUMN';
                ALTER SCHEMA [dbo] TRANSFER [ORP].[WorkflowSteps];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[WorkflowDefinitions];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Users];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[UserRoles];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[UserDepartments];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[UserBranches];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Roles];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[RolePermissions];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Reviews];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Permissions];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Messages];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Departments];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Branches];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[AuditEvents];
                ALTER SCHEMA [dbo] TRANSFER [ORP].[Assignments];
                """);

            migrationBuilder.AddColumn<string>(
                name: "Account",
                table: "Messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Messages",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Messages",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "Messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OwningDepartmentId",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedAt",
                table: "Messages",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Receiver",
                table: "Messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Messages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sender",
                table: "Messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "MessageRawData",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    RawContent = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRawData", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_MessageRawData_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_BranchId_OwningDepartmentId_ReceivedAt_Id",
                table: "Messages",
                columns: new[] { "BranchId", "OwningDepartmentId", "ReceivedAt", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_BranchId_OwningDepartmentId_State_ReceivedAt_Id",
                table: "Messages",
                columns: new[] { "BranchId", "OwningDepartmentId", "State", "ReceivedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ExternalId",
                table: "Messages",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_OwningDepartmentId",
                table: "Messages",
                column: "OwningDepartmentId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Branches_BranchId",
                table: "Messages",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Departments_OwningDepartmentId",
                table: "Messages",
                column: "OwningDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
