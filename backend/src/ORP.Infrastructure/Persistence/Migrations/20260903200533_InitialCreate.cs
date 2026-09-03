using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ORP");

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ORP",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "ORP",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "ORP",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "ORP",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "ORP",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBranches",
                schema: "ORP",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranches", x => new { x.UserId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_UserBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ORP",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDepartments",
                schema: "ORP",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartments", x => new { x.UserId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_UserDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "ORP",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "ORP",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "ORP",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "ORP",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CurrentAssigneeId = table.Column<int>(type: "int", nullable: true),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Messages_Users_CurrentAssigneeId",
                        column: x => x.CurrentAssigneeId,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "ORP",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ReviewLevel = table.Column<int>(type: "int", nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "ORP",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assignments",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedBy = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "ORP",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Users_AssignedTo",
                        column: x => x.AssignedTo,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OldState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NewState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "ORP",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "ORP",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ReviewerId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "ORP",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalSchema: "ORP",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignedBy",
                schema: "ORP",
                table: "Assignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignedTo",
                schema: "ORP",
                table: "Assignments",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MessageId",
                schema: "ORP",
                table: "Assignments",
                column: "MessageId",
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_MessageId_Timestamp",
                schema: "ORP",
                table: "AuditEvents",
                columns: new[] { "MessageId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_UserId",
                schema: "ORP",
                table: "AuditEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CurrentAssigneeId",
                schema: "ORP",
                table: "Messages",
                column: "CurrentAssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_WorkflowDefinitionId",
                schema: "ORP",
                table: "Messages",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                schema: "ORP",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MessageId_Level",
                schema: "ORP",
                table: "Reviews",
                columns: new[] { "MessageId", "Level" },
                unique: true,
                filter: "[Status] <> N'Undone'");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId",
                schema: "ORP",
                table: "Reviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                schema: "ORP",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "ORP",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_BranchId",
                schema: "ORP",
                table: "UserBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_DepartmentId",
                schema: "ORP",
                table: "UserDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "ORP",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                schema: "ORP",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_BranchId",
                schema: "ORP",
                table: "WorkflowDefinitions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_DepartmentId",
                schema: "ORP",
                table: "WorkflowDefinitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId",
                schema: "ORP",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "DepartmentId", "BranchId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowDefinitionId_Order",
                schema: "ORP",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowDefinitionId", "Order" },
                unique: true);

            migrationBuilder.Sql(
                """
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
                WHERE 1 = 0;
                """);

            migrationBuilder.Sql(
                """
                CREATE PROCEDURE [ORP].[RegisterNewMessages]
                AS
                BEGIN
                    SET NOCOUNT ON;

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
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [ORP].[RegisterNewMessages];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [ORP].[SwiftMessageSource];");

            migrationBuilder.DropTable(
                name: "Assignments",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "UserBranches",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "UserDepartments",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "WorkflowSteps",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "Branches",
                schema: "ORP");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "ORP");
        }
    }
}
