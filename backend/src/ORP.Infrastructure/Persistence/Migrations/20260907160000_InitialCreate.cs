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
                name: "orp");

            migrationBuilder.CreateTable(
                name: "Branches",
                schema: "orp",
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
                schema: "orp",
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
                schema: "orp",
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
                schema: "orp",
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
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IsGlobalAdministrator = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SwiftMessages",
                schema: "orp",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BodyContainsMx = table.Column<bool>(type: "bit", nullable: false),
                    BodyContainsMt = table.Column<bool>(type: "bit", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BackendDirection = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CounterParty = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CounterPartyCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    LastModificationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MessageDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MessageLength = table.Column<int>(type: "int", nullable: false),
                    MessageFormatVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MessageInputReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MessageTypeShort = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceiverResponder = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SenderRequestor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SequenceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SessionNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Service = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceInterface = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StatusDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    RoutingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RoutingError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastSynchronizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwiftMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_SwiftMessages_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "orp",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwiftMessages_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                schema: "orp",
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
                        principalSchema: "orp",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "orp",
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
                        principalSchema: "orp",
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "orp",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessAuditEvents",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorId = table.Column<int>(type: "int", nullable: false),
                    TargetUserId = table.Column<int>(type: "int", nullable: true),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessAuditEvents_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "orp",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessAuditEvents_Users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessAuditEvents_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "orp",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.BranchId, x.DepartmentId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "orp",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "orp",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "orp",
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
                        name: "FK_Messages_SwiftMessages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "orp",
                        principalTable: "SwiftMessages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Users_CurrentAssigneeId",
                        column: x => x.CurrentAssigneeId,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "orp",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
                schema: "orp",
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
                        principalSchema: "orp",
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assignments",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedBy = table.Column<int>(type: "int", nullable: true),
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
                        principalSchema: "orp",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Users_AssignedTo",
                        column: x => x.AssignedTo,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "orp",
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
                        principalSchema: "orp",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "orp",
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
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReviewId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "orp",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalSchema: "orp",
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditEvents_ActorId",
                schema: "orp",
                table: "AccessAuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditEvents_RoleId_Timestamp",
                schema: "orp",
                table: "AccessAuditEvents",
                columns: new[] { "RoleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditEvents_TargetUserId_Timestamp",
                schema: "orp",
                table: "AccessAuditEvents",
                columns: new[] { "TargetUserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignedBy",
                schema: "orp",
                table: "Assignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignedTo",
                schema: "orp",
                table: "Assignments",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MessageId",
                schema: "orp",
                table: "Assignments",
                column: "MessageId",
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_MessageId_Timestamp",
                schema: "orp",
                table: "AuditEvents",
                columns: new[] { "MessageId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ReviewId",
                schema: "orp",
                table: "AuditEvents",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_UserId",
                schema: "orp",
                table: "AuditEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CurrentAssigneeId",
                schema: "orp",
                table: "Messages",
                column: "CurrentAssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_WorkflowDefinitionId",
                schema: "orp",
                table: "Messages",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                schema: "orp",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MessageId_Level",
                schema: "orp",
                table: "Reviews",
                columns: new[] { "MessageId", "Level" },
                unique: true,
                filter: "[Status] <> N'Undone' AND [Status] <> N'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId",
                schema: "orp",
                table: "Reviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                schema: "orp",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                schema: "orp",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwiftMessages_BranchId",
                schema: "orp",
                table: "SwiftMessages",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SwiftMessages_DepartmentId",
                schema: "orp",
                table: "SwiftMessages",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SwiftMessages_WarehouseId",
                schema: "orp",
                table: "SwiftMessages",
                column: "WarehouseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_BranchId",
                schema: "orp",
                table: "UserRoles",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_DepartmentId",
                schema: "orp",
                table: "UserRoles",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "orp",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                schema: "orp",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_DepartmentId",
                schema: "orp",
                table: "WorkflowDefinitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId",
                schema: "orp",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "DepartmentId", "BranchId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowDefinitionId_Order",
                schema: "orp",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowDefinitionId", "Order" },
                unique: true);
            migrationBuilder.Sql(
                """
                INSERT INTO [orp].[Permissions] ([Name]) VALUES
                    (N'message.view'), (N'message.assign'), (N'review.level1'),
                    (N'review.level2'), (N'review.level3'),
                    (N'review.undo'), (N'audit.view'), (N'workflow.manage');
                INSERT INTO [orp].[Roles] ([Name]) VALUES
                    (N'CS Reviewer'), (N'TFO Reviewer'), (N'DC Reviewer'),
                    (N'DC Senior Reviewer'), (N'Operations manager');
                INSERT INTO [orp].[RolePermissions] ([RoleId], [PermissionId])
                SELECT role.[Id], permission.[Id]
                FROM [orp].[Roles] AS role CROSS JOIN [orp].[Permissions] AS permission
                WHERE role.[Name] = N'Operations manager'
                   OR permission.[Name] = N'audit.view'
                   OR (role.[Name] IN (N'CS Reviewer', N'DC Reviewer')
                       AND permission.[Name] IN (N'message.view', N'review.level1'))
                   OR (role.[Name] = N'TFO Reviewer'
                       AND permission.[Name] IN (N'message.view', N'review.level1', N'review.level2'))
                   OR (role.[Name] = N'DC Senior Reviewer'
                       AND permission.[Name] IN (N'message.view', N'review.level2', N'review.level3', N'review.undo'));
                """);
            migrationBuilder.Sql(RegisterNewMessagesSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [orp].[RegisterNewMessages];");
            migrationBuilder.DropTable(
                name: "AccessAuditEvents",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Assignments",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "WorkflowSteps",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Permissions",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "SwiftMessages",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Branches",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "orp");
        }
        private const string RegisterNewMessagesSql =
            """
            CREATE PROCEDURE [orp].[RegisterNewMessages]
                @CorrelationId nvarchar(100) = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @StartedTransaction bit = 0;
                DECLARE @RegisteredAt datetimeoffset = SYSUTCDATETIME();
                DECLARE @EffectiveCorrelationId nvarchar(100) = COALESCE(
                    NULLIF(LTRIM(RTRIM(@CorrelationId)), N''),
                    CONCAT(N'registration-', CONVERT(nvarchar(36), NEWID())));
                DECLARE @Registered TABLE
                (
                    [MessageId] bigint NOT NULL,
                    [State] nvarchar(40) NOT NULL,
                    [WorkflowDefinitionId] int NOT NULL
                );

                IF @@TRANCOUNT = 0
                BEGIN
                    BEGIN TRANSACTION;
                    SET @StartedTransaction = 1;
                END;

                BEGIN TRY
                    INSERT INTO [orp].[Messages]
                        ([MessageId], [State], [CurrentAssigneeId], [WorkflowDefinitionId])
                    OUTPUT inserted.[MessageId], inserted.[State], inserted.[WorkflowDefinitionId]
                        INTO @Registered ([MessageId], [State], [WorkflowDefinitionId])
                    SELECT source.[MessageId], N'New', NULL, workflow.[Id]
                    FROM [orp].[SwiftMessages] AS source
                    CROSS APPLY
                    (
                        SELECT TOP (1) candidate.[Id]
                        FROM [orp].[WorkflowDefinitions] AS candidate
                        WHERE candidate.[IsActive] = 1
                          AND candidate.[MessageType] = source.[MessageType]
                          AND candidate.[DepartmentId] = source.[DepartmentId]
                          AND (candidate.[BranchId] = source.[BranchId] OR candidate.[BranchId] IS NULL)
                          AND EXISTS
                          (
                              SELECT 1 FROM [orp].[WorkflowSteps] AS requiredStep
                              WHERE requiredStep.[WorkflowDefinitionId] = candidate.[Id]
                                AND requiredStep.[Required] = 1
                                AND requiredStep.[ReviewLevel] = 1
                          )
                          AND NOT EXISTS
                          (
                              SELECT 1 FROM [orp].[WorkflowSteps] AS step
                              WHERE step.[WorkflowDefinitionId] = candidate.[Id]
                                AND step.[ReviewLevel] NOT BETWEEN 1 AND 3
                          )
                          AND NOT EXISTS
                          (
                              SELECT step.[ReviewLevel]
                              FROM [orp].[WorkflowSteps] AS step
                              WHERE step.[WorkflowDefinitionId] = candidate.[Id]
                              GROUP BY step.[ReviewLevel]
                              HAVING COUNT(*) > 1
                          )
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM [orp].[WorkflowSteps] AS earlier
                              INNER JOIN [orp].[WorkflowSteps] AS later
                                  ON later.[WorkflowDefinitionId] = earlier.[WorkflowDefinitionId]
                                 AND later.[Order] > earlier.[Order]
                              WHERE earlier.[WorkflowDefinitionId] = candidate.[Id]
                                AND earlier.[Required] = 1
                                AND later.[Required] = 1
                                AND later.[ReviewLevel] <= earlier.[ReviewLevel]
                          )
                        ORDER BY CASE WHEN candidate.[BranchId] = source.[BranchId] THEN 0 ELSE 1 END,
                            candidate.[Id]
                    ) AS workflow
                    WHERE source.[RoutingStatus] = N'Routed'
                      AND source.[BranchId] IS NOT NULL
                      AND source.[DepartmentId] IS NOT NULL
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM [orp].[Messages] AS existing WITH (UPDLOCK, HOLDLOCK)
                          WHERE existing.[MessageId] = source.[MessageId]
                      );

                    INSERT INTO [orp].[AuditEvents]
                        ([MessageId], [EventType], [UserId], [Timestamp], [OldState], [NewState],
                         [DetailsJson], [CorrelationId], [ReviewId])
                    SELECT registered.[MessageId], N'MessageRegistered', NULL, @RegisteredAt, NULL,
                        registered.[State],
                        CONCAT(N'{"workflowDefinitionId":', registered.[WorkflowDefinitionId], N'}'),
                        @EffectiveCorrelationId, NULL
                    FROM @Registered AS registered;

                    IF @StartedTransaction = 1 COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF @StartedTransaction = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
            END;
            """;
    }
}
