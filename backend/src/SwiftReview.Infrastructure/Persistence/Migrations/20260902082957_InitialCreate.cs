using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SwiftReview.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Branches",
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
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
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
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowDefinitions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
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
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBranches",
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
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDepartments",
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
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
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
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OwningDepartmentId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentAssigneeId = table.Column<int>(type: "int", nullable: true),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Sender = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Receiver = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Departments_OwningDepartmentId",
                        column: x => x.OwningDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Users_CurrentAssigneeId",
                        column: x => x.CurrentAssigneeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
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
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assignments",
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
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Users_AssignedTo",
                        column: x => x.AssignedTo,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
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
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "Reviews",
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
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "London" },
                    { 2, "Dublin" },
                    { 3, "Singapore" }
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "CS" },
                    { 2, "TFO" },
                    { 3, "DC" }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "message.view" },
                    { 2, "message.assign" },
                    { 3, "review.level1" },
                    { 4, "review.level2" },
                    { 5, "review.level3" },
                    { 6, "review.reject" },
                    { 7, "review.undo" },
                    { 8, "audit.view" },
                    { 9, "workflow.manage" },
                    { 10, "message.import" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "CS Reviewer" },
                    { 2, "TFO Reviewer" },
                    { 3, "DC Reviewer" },
                    { 4, "DC Senior Reviewer" },
                    { 5, "Supervisor" },
                    { 6, "Administrator" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "DisplayName", "UserName" },
                values: new object[,]
                {
                    { 1, "CS Reviewer", "cs-reviewer" },
                    { 2, "TFO Reviewer", "tfo-reviewer" },
                    { 3, "DC Reviewer", "dc-reviewer" },
                    { 4, "DC Senior Reviewer", "dc-senior" },
                    { 5, "Supervisor", "supervisor" },
                    { 6, "Administrator", "admin" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 3, 1 },
                    { 1, 2 },
                    { 3, 2 },
                    { 4, 2 },
                    { 1, 3 },
                    { 3, 3 },
                    { 1, 4 },
                    { 4, 4 },
                    { 5, 4 },
                    { 6, 4 },
                    { 7, 4 },
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 4, 5 },
                    { 5, 5 },
                    { 6, 5 },
                    { 7, 5 },
                    { 8, 5 },
                    { 1, 6 },
                    { 2, 6 },
                    { 3, 6 },
                    { 4, 6 },
                    { 5, 6 },
                    { 6, 6 },
                    { 7, 6 },
                    { 8, 6 },
                    { 9, 6 },
                    { 10, 6 }
                });

            migrationBuilder.InsertData(
                table: "UserBranches",
                columns: new[] { "BranchId", "UserId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 2 },
                    { 3, 3 },
                    { 1, 4 },
                    { 2, 4 },
                    { 3, 4 },
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 1, 6 },
                    { 2, 6 },
                    { 3, 6 }
                });

            migrationBuilder.InsertData(
                table: "UserDepartments",
                columns: new[] { "DepartmentId", "UserId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 2 },
                    { 3, 3 },
                    { 1, 4 },
                    { 2, 4 },
                    { 3, 4 },
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 1, 6 },
                    { 2, 6 },
                    { 3, 6 }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 2 },
                    { 3, 3 },
                    { 4, 4 },
                    { 5, 5 },
                    { 6, 6 }
                });

            migrationBuilder.InsertData(
                table: "WorkflowDefinitions",
                columns: new[] { "Id", "BranchId", "DepartmentId", "IsActive", "MessageType", "Name" },
                values: new object[,]
                {
                    { 1, null, 1, true, "MT199", "Single Review" },
                    { 2, null, 2, true, "MT299", "Two Reviews" },
                    { 3, null, 3, true, "MT671", "Three Reviews" },
                    { 4, null, 1, true, "MT700", "MT700 Single Review" },
                    { 5, null, 2, true, "MT710", "MT710 Two Reviews" },
                    { 6, null, 3, true, "MT760", "MT760 Three Reviews" },
                    { 7, null, 1, true, "MT799", "MT799 Single Review" },
                    { 8, null, 2, true, "MT999", "MT999 Two Reviews" }
                });

            migrationBuilder.InsertData(
                table: "Messages",
                columns: new[] { "Id", "Account", "Amount", "BranchId", "Currency", "CurrentAssigneeId", "ExternalId", "MessageType", "OwningDepartmentId", "ReceivedAt", "Receiver", "Reference", "Sender", "State", "WorkflowDefinitionId" },
                values: new object[,]
                {
                    { 1L, "ACCT-00001", 1017.25m, 1, "EUR", null, "SEED-0001", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0001", "BANK01", "New", 1 },
                    { 2L, "ACCT-00002", 1034.50m, 2, "USD", null, "SEED-0002", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 1, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0002", "BANK02", "New", 2 },
                    { 3L, "ACCT-00003", 1051.75m, 3, "GBP", null, "SEED-0003", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0003", "BANK03", "New", 3 },
                    { 4L, "ACCT-00004", 1069.00m, 1, "EUR", null, "SEED-0004", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0004", "BANK04", "New", 4 },
                    { 5L, "ACCT-00005", 1086.25m, 2, "USD", null, "SEED-0005", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 1, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0005", "BANK05", "New", 5 },
                    { 6L, "ACCT-00006", 1103.50m, 3, "GBP", null, "SEED-0006", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 1, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0006", "BANK06", "New", 6 },
                    { 7L, "ACCT-00007", 1120.75m, 1, "EUR", null, "SEED-0007", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0007", "BANK07", "New", 7 },
                    { 8L, "ACCT-00008", 1138.00m, 2, "USD", null, "SEED-0008", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 1, 16, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0008", "BANK08", "New", 8 },
                    { 9L, "ACCT-00009", 1155.25m, 3, "GBP", 5, "SEED-0009", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 1, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0009", "BANK00", "Assigned", 1 },
                    { 10L, "ACCT-00010", 1172.50m, 1, "EUR", 5, "SEED-0010", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 1, 18, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0010", "BANK01", "Assigned", 2 },
                    { 11L, "ACCT-00011", 1189.75m, 2, "USD", 5, "SEED-0011", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 1, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0011", "BANK02", "Assigned", 3 },
                    { 12L, "ACCT-00012", 1207.00m, 3, "GBP", 5, "SEED-0012", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 1, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0012", "BANK03", "Assigned", 4 },
                    { 13L, "ACCT-00013", 1224.25m, 1, "EUR", 5, "SEED-0013", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 1, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0013", "BANK04", "Assigned", 5 },
                    { 14L, "ACCT-00014", 1241.50m, 2, "USD", 5, "SEED-0014", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 1, 22, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0014", "BANK05", "Assigned", 6 },
                    { 15L, "ACCT-00015", 1258.75m, 3, "GBP", 5, "SEED-0015", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 1, 23, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0015", "BANK06", "Assigned", 7 },
                    { 16L, "ACCT-00016", 1276.00m, 1, "EUR", 5, "SEED-0016", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0016", "BANK07", "Assigned", 8 },
                    { 17L, "ACCT-00017", 1293.25m, 2, "USD", 5, "SEED-0017", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 1, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0017", "BANK08", "FirstReviewInProgress", 1 },
                    { 18L, "ACCT-00018", 1310.50m, 3, "GBP", 5, "SEED-0018", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0018", "BANK00", "FirstReviewInProgress", 2 },
                    { 19L, "ACCT-00019", 1327.75m, 1, "EUR", 5, "SEED-0019", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 2, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0019", "BANK01", "FirstReviewInProgress", 3 },
                    { 20L, "ACCT-00020", 1345.00m, 2, "USD", 5, "SEED-0020", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0020", "BANK02", "FirstReviewInProgress", 4 },
                    { 21L, "ACCT-00021", 1362.25m, 3, "GBP", 5, "SEED-0021", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 5, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0021", "BANK03", "FirstReviewInProgress", 5 },
                    { 22L, "ACCT-00022", 1379.50m, 1, "EUR", 5, "SEED-0022", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 2, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0022", "BANK04", "FirstReviewInProgress", 6 },
                    { 23L, "ACCT-00023", 1396.75m, 2, "USD", 5, "SEED-0023", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 7, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0023", "BANK05", "FirstReviewInProgress", 7 },
                    { 24L, "ACCT-00024", 1414.00m, 3, "GBP", 5, "SEED-0024", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0024", "BANK06", "FirstReviewInProgress", 8 },
                    { 25L, "ACCT-00025", 1431.25m, 1, "EUR", 5, "SEED-0025", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0025", "BANK07", "Completed", 1 },
                    { 26L, "ACCT-00026", 1448.50m, 2, "USD", 6, "SEED-0026", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0026", "BANK08", "WaitingForSecondReview", 2 },
                    { 27L, "ACCT-00027", 1465.75m, 3, "GBP", 6, "SEED-0027", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 2, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0027", "BANK00", "WaitingForSecondReview", 3 },
                    { 28L, "ACCT-00028", 1483.00m, 1, "EUR", 5, "SEED-0028", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0028", "BANK01", "Completed", 4 },
                    { 29L, "ACCT-00029", 1500.25m, 2, "USD", 6, "SEED-0029", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0029", "BANK02", "WaitingForSecondReview", 5 },
                    { 30L, "ACCT-00030", 1517.50m, 3, "GBP", 6, "SEED-0030", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0030", "BANK03", "WaitingForSecondReview", 6 },
                    { 31L, "ACCT-00031", 1534.75m, 1, "EUR", 5, "SEED-0031", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0031", "BANK04", "Completed", 7 },
                    { 32L, "ACCT-00032", 1552.00m, 2, "USD", 6, "SEED-0032", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 16, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0032", "BANK05", "WaitingForSecondReview", 8 },
                    { 33L, "ACCT-00033", 1569.25m, 3, "GBP", null, "SEED-0033", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0033", "BANK06", "New", 1 },
                    { 34L, "ACCT-00034", 1586.50m, 1, "EUR", 6, "SEED-0034", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 18, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0034", "BANK07", "SecondReviewInProgress", 2 },
                    { 35L, "ACCT-00035", 1603.75m, 2, "USD", 6, "SEED-0035", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 2, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0035", "BANK08", "SecondReviewInProgress", 3 },
                    { 36L, "ACCT-00036", 1621.00m, 3, "GBP", null, "SEED-0036", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0036", "BANK00", "New", 4 },
                    { 37L, "ACCT-00037", 1638.25m, 1, "EUR", 6, "SEED-0037", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0037", "BANK01", "SecondReviewInProgress", 5 },
                    { 38L, "ACCT-00038", 1655.50m, 2, "USD", 6, "SEED-0038", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 2, 22, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0038", "BANK02", "SecondReviewInProgress", 6 },
                    { 39L, "ACCT-00039", 1672.75m, 3, "GBP", null, "SEED-0039", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 2, 23, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0039", "BANK03", "New", 7 },
                    { 40L, "ACCT-00040", 1690.00m, 1, "EUR", 6, "SEED-0040", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0040", "BANK04", "SecondReviewInProgress", 8 },
                    { 41L, "ACCT-00041", 1707.25m, 2, "USD", 5, "SEED-0041", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0041", "BANK05", "Assigned", 1 },
                    { 42L, "ACCT-00042", 1724.50m, 3, "GBP", 6, "SEED-0042", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0042", "BANK06", "Completed", 2 },
                    { 43L, "ACCT-00043", 1741.75m, 1, "EUR", 4, "SEED-0043", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 3, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0043", "BANK07", "WaitingForThirdReview", 3 },
                    { 44L, "ACCT-00044", 1759.00m, 2, "USD", 5, "SEED-0044", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0044", "BANK08", "Assigned", 4 },
                    { 45L, "ACCT-00045", 1776.25m, 3, "GBP", 6, "SEED-0045", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 5, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0045", "BANK00", "Completed", 5 },
                    { 46L, "ACCT-00046", 1793.50m, 1, "EUR", 4, "SEED-0046", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 3, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0046", "BANK01", "WaitingForThirdReview", 6 },
                    { 47L, "ACCT-00047", 1810.75m, 2, "USD", 5, "SEED-0047", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 7, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0047", "BANK02", "Assigned", 7 },
                    { 48L, "ACCT-00048", 1828.00m, 3, "GBP", 6, "SEED-0048", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0048", "BANK03", "Completed", 8 },
                    { 49L, "ACCT-00049", 1845.25m, 1, "EUR", 5, "SEED-0049", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0049", "BANK04", "FirstReviewInProgress", 1 },
                    { 50L, "ACCT-00050", 1862.50m, 2, "USD", null, "SEED-0050", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0050", "BANK05", "New", 2 },
                    { 51L, "ACCT-00051", 1879.75m, 3, "GBP", 4, "SEED-0051", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 3, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0051", "BANK06", "ThirdReviewInProgress", 3 },
                    { 52L, "ACCT-00052", 1897.00m, 1, "EUR", 5, "SEED-0052", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0052", "BANK07", "FirstReviewInProgress", 4 },
                    { 53L, "ACCT-00053", 1914.25m, 2, "USD", null, "SEED-0053", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0053", "BANK08", "New", 5 },
                    { 54L, "ACCT-00054", 1931.50m, 3, "GBP", 4, "SEED-0054", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 3, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0054", "BANK00", "ThirdReviewInProgress", 6 },
                    { 55L, "ACCT-00055", 1948.75m, 1, "EUR", 5, "SEED-0055", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0055", "BANK01", "FirstReviewInProgress", 7 },
                    { 56L, "ACCT-00056", 1966.00m, 2, "USD", null, "SEED-0056", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 16, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0056", "BANK02", "New", 8 },
                    { 57L, "ACCT-00057", 1983.25m, 3, "GBP", 5, "SEED-0057", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0057", "BANK03", "Completed", 1 },
                    { 58L, "ACCT-00058", 2000.50m, 1, "EUR", 5, "SEED-0058", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 18, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0058", "BANK04", "Assigned", 2 },
                    { 59L, "ACCT-00059", 2017.75m, 2, "USD", 4, "SEED-0059", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0059", "BANK05", "Completed", 3 },
                    { 60L, "ACCT-00060", 2035.00m, 3, "GBP", 5, "SEED-0060", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0060", "BANK06", "Completed", 4 },
                    { 61L, "ACCT-00061", 2052.25m, 1, "EUR", 5, "SEED-0061", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 3, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0061", "BANK07", "Assigned", 5 },
                    { 62L, "ACCT-00062", 2069.50m, 2, "USD", 4, "SEED-0062", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0062", "BANK08", "Completed", 6 },
                    { 63L, "ACCT-00063", 2086.75m, 3, "GBP", 5, "SEED-0063", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 3, 23, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0063", "BANK00", "Completed", 7 },
                    { 64L, "ACCT-00064", 2104.00m, 1, "EUR", 5, "SEED-0064", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0064", "BANK01", "Assigned", 8 },
                    { 65L, "ACCT-00065", 2121.25m, 2, "USD", null, "SEED-0065", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 4, 1, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0065", "BANK02", "New", 1 },
                    { 66L, "ACCT-00066", 2138.50m, 3, "GBP", 5, "SEED-0066", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 4, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0066", "BANK03", "FirstReviewInProgress", 2 },
                    { 67L, "ACCT-00067", 2155.75m, 1, "EUR", null, "SEED-0067", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 4, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0067", "BANK04", "New", 3 },
                    { 68L, "ACCT-00068", 2173.00m, 2, "USD", null, "SEED-0068", "MT700", 1, new DateTimeOffset(new DateTime(2026, 8, 4, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0068", "BANK05", "New", 4 },
                    { 69L, "ACCT-00069", 2190.25m, 3, "GBP", 5, "SEED-0069", "MT710", 2, new DateTimeOffset(new DateTime(2026, 8, 4, 5, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0069", "BANK06", "FirstReviewInProgress", 5 },
                    { 70L, "ACCT-00070", 2207.50m, 1, "EUR", null, "SEED-0070", "MT760", 3, new DateTimeOffset(new DateTime(2026, 8, 4, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0070", "BANK07", "New", 6 },
                    { 71L, "ACCT-00071", 2224.75m, 2, "USD", null, "SEED-0071", "MT799", 1, new DateTimeOffset(new DateTime(2026, 8, 4, 7, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0071", "BANK08", "New", 7 },
                    { 72L, "ACCT-00072", 2242.00m, 3, "GBP", 5, "SEED-0072", "MT999", 2, new DateTimeOffset(new DateTime(2026, 8, 4, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0072", "BANK00", "FirstReviewInProgress", 8 },
                    { 73L, "ACCT-00073", 2259.25m, 1, "EUR", 5, "SEED-0073", "MT199", 1, new DateTimeOffset(new DateTime(2026, 8, 4, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0073", "BANK01", "Assigned", 1 },
                    { 74L, "ACCT-00074", 2276.50m, 2, "USD", 6, "SEED-0074", "MT299", 2, new DateTimeOffset(new DateTime(2026, 8, 4, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0074", "BANK02", "WaitingForSecondReview", 2 },
                    { 75L, "ACCT-00075", 2293.75m, 3, "GBP", 5, "SEED-0075", "MT671", 3, new DateTimeOffset(new DateTime(2026, 8, 4, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SWIFTREVIEW", "REF-0075", "BANK03", "Assigned", 3 }
                });

            migrationBuilder.InsertData(
                table: "WorkflowSteps",
                columns: new[] { "Id", "Order", "Required", "ReviewLevel", "WorkflowDefinitionId" },
                values: new object[,]
                {
                    { 1, 1, true, 1, 1 },
                    { 2, 1, true, 1, 2 },
                    { 3, 2, true, 2, 2 },
                    { 4, 1, true, 1, 3 },
                    { 5, 2, true, 2, 3 },
                    { 6, 3, true, 3, 3 },
                    { 7, 1, true, 1, 4 },
                    { 8, 1, true, 1, 5 },
                    { 9, 2, true, 2, 5 },
                    { 10, 1, true, 1, 6 },
                    { 11, 2, true, 2, 6 },
                    { 12, 3, true, 3, 6 },
                    { 13, 1, true, 1, 7 },
                    { 14, 1, true, 1, 8 },
                    { 15, 2, true, 2, 8 }
                });

            migrationBuilder.InsertData(
                table: "Assignments",
                columns: new[] { "Id", "AssignedBy", "AssignedTo", "CreatedAt", "EndedAt", "MessageId" },
                values: new object[,]
                {
                    { 9L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 1, 17, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 9L },
                    { 10L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 1, 18, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 10L },
                    { 11L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 1, 19, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 11L },
                    { 12L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 1, 20, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 12L },
                    { 13L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 1, 21, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 13L },
                    { 14L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 1, 22, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 14L },
                    { 15L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 1, 23, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 15L },
                    { 16L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 16L },
                    { 17L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 1, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 17L },
                    { 18L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 2, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 18L },
                    { 19L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 3, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 19L },
                    { 20L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 4, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 20L },
                    { 21L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 5, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 21L },
                    { 22L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 6, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 22L },
                    { 23L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 7, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 23L },
                    { 24L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 8, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 24L },
                    { 25L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 9, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 25L },
                    { 26L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 10, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 26L },
                    { 27L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 11, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 27L },
                    { 28L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 12, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 28L },
                    { 29L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 13, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 29L },
                    { 30L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 14, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 30L },
                    { 31L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 15, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 31L },
                    { 32L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 16, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 32L },
                    { 34L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 18, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 34L },
                    { 35L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 19, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 35L },
                    { 37L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 21, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 37L },
                    { 38L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 22, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 38L },
                    { 40L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 40L },
                    { 41L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 1, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 41L },
                    { 42L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 2, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 42L },
                    { 43L, 6, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 3, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 43L },
                    { 44L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 4, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 44L },
                    { 45L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 5, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 45L },
                    { 46L, 6, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 6, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 46L },
                    { 47L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 7, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 47L },
                    { 48L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 8, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 48L },
                    { 49L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 9, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 49L },
                    { 51L, 6, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 11, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 51L },
                    { 52L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 12, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 52L },
                    { 54L, 6, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 14, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 54L },
                    { 55L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 15, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 55L },
                    { 57L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 17, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 57L },
                    { 58L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 18, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 58L },
                    { 59L, 6, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 59L },
                    { 60L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 20, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 60L },
                    { 61L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 21, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 61L },
                    { 62L, 6, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 62L },
                    { 63L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 23, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 63L },
                    { 64L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 64L },
                    { 66L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 2, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 66L },
                    { 69L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 5, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 69L },
                    { 72L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 8, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 72L },
                    { 73L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 9, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 73L },
                    { 74L, 5, 6, new DateTimeOffset(new DateTime(2026, 8, 4, 10, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 74L },
                    { 75L, 6, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 11, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 75L }
                });

            migrationBuilder.InsertData(
                table: "AuditEvents",
                columns: new[] { "Id", "CorrelationId", "DetailsJson", "EventType", "MessageId", "NewState", "OldState", "Timestamp", "UserId" },
                values: new object[,]
                {
                    { 101L, "seed-0001", "{}", "MessageImported", 1L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 201L, "seed-0002", "{}", "MessageImported", 2L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 301L, "seed-0003", "{}", "MessageImported", 3L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 401L, "seed-0004", "{}", "MessageImported", 4L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 501L, "seed-0005", "{}", "MessageImported", 5L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 601L, "seed-0006", "{}", "MessageImported", 6L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 701L, "seed-0007", "{}", "MessageImported", 7L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 801L, "seed-0008", "{}", "MessageImported", 8L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 16, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 901L, "seed-0009", "{}", "MessageImported", 9L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 902L, "seed-0009", "{}", "MessageAssigned", 9L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 1, 17, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1001L, "seed-0010", "{}", "MessageImported", 10L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 18, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1002L, "seed-0010", "{}", "MessageAssigned", 10L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 1, 18, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1101L, "seed-0011", "{}", "MessageImported", 11L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1102L, "seed-0011", "{}", "MessageAssigned", 11L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 1, 19, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1201L, "seed-0012", "{}", "MessageImported", 12L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1202L, "seed-0012", "{}", "MessageAssigned", 12L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 1, 20, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1301L, "seed-0013", "{}", "MessageImported", 13L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1302L, "seed-0013", "{}", "MessageAssigned", 13L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 1, 21, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1401L, "seed-0014", "{}", "MessageImported", 14L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 22, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1402L, "seed-0014", "{}", "MessageAssigned", 14L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 1, 22, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1501L, "seed-0015", "{}", "MessageImported", 15L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 1, 23, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1502L, "seed-0015", "{}", "MessageAssigned", 15L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 1, 23, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1601L, "seed-0016", "{}", "MessageImported", 16L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1602L, "seed-0016", "{}", "MessageAssigned", 16L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1701L, "seed-0017", "{}", "MessageImported", 17L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 1, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1702L, "seed-0017", "{}", "MessageAssigned", 17L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 1, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1703L, "seed-0017", "{\"level\":1}", "ReviewStarted", 17L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 1, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 1801L, "seed-0018", "{}", "MessageImported", 18L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1802L, "seed-0018", "{}", "MessageAssigned", 18L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 2, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1803L, "seed-0018", "{\"level\":1}", "ReviewStarted", 18L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 2, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 1901L, "seed-0019", "{}", "MessageImported", 19L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 1902L, "seed-0019", "{}", "MessageAssigned", 19L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 3, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 1903L, "seed-0019", "{\"level\":1}", "ReviewStarted", 19L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 3, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2001L, "seed-0020", "{}", "MessageImported", 20L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2002L, "seed-0020", "{}", "MessageAssigned", 20L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 4, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2003L, "seed-0020", "{\"level\":1}", "ReviewStarted", 20L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 4, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2101L, "seed-0021", "{}", "MessageImported", 21L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 5, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2102L, "seed-0021", "{}", "MessageAssigned", 21L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 5, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2103L, "seed-0021", "{\"level\":1}", "ReviewStarted", 21L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 5, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2201L, "seed-0022", "{}", "MessageImported", 22L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2202L, "seed-0022", "{}", "MessageAssigned", 22L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 6, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2203L, "seed-0022", "{\"level\":1}", "ReviewStarted", 22L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 6, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2301L, "seed-0023", "{}", "MessageImported", 23L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 7, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2302L, "seed-0023", "{}", "MessageAssigned", 23L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 7, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2303L, "seed-0023", "{\"level\":1}", "ReviewStarted", 23L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 7, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2401L, "seed-0024", "{}", "MessageImported", 24L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2402L, "seed-0024", "{}", "MessageAssigned", 24L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 8, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2403L, "seed-0024", "{\"level\":1}", "ReviewStarted", 24L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 8, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2501L, "seed-0025", "{}", "MessageImported", 25L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2502L, "seed-0025", "{}", "MessageAssigned", 25L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 9, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2503L, "seed-0025", "{\"level\":1}", "ReviewStarted", 25L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 9, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2504L, "seed-0025", "{\"level\":1}", "MessageCompleted", 25L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 9, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2601L, "seed-0026", "{}", "MessageImported", 26L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2602L, "seed-0026", "{}", "MessageAssigned", 26L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 10, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2603L, "seed-0026", "{\"level\":1}", "ReviewStarted", 26L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 10, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2604L, "seed-0026", "{\"level\":1}", "ReviewApproved", 26L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 10, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2701L, "seed-0027", "{}", "MessageImported", 27L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2702L, "seed-0027", "{}", "MessageAssigned", 27L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 11, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2703L, "seed-0027", "{\"level\":1}", "ReviewStarted", 27L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 11, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2704L, "seed-0027", "{\"level\":1}", "ReviewApproved", 27L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 11, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2801L, "seed-0028", "{}", "MessageImported", 28L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2802L, "seed-0028", "{}", "MessageAssigned", 28L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 12, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2803L, "seed-0028", "{\"level\":1}", "ReviewStarted", 28L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 12, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2804L, "seed-0028", "{\"level\":1}", "MessageCompleted", 28L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 12, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2901L, "seed-0029", "{}", "MessageImported", 29L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 2902L, "seed-0029", "{}", "MessageAssigned", 29L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 13, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 2903L, "seed-0029", "{\"level\":1}", "ReviewStarted", 29L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 13, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 2904L, "seed-0029", "{\"level\":1}", "ReviewApproved", 29L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 13, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3001L, "seed-0030", "{}", "MessageImported", 30L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3002L, "seed-0030", "{}", "MessageAssigned", 30L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 14, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3003L, "seed-0030", "{\"level\":1}", "ReviewStarted", 30L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 14, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3004L, "seed-0030", "{\"level\":1}", "ReviewApproved", 30L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 14, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3101L, "seed-0031", "{}", "MessageImported", 31L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3102L, "seed-0031", "{}", "MessageAssigned", 31L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 15, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3103L, "seed-0031", "{\"level\":1}", "ReviewStarted", 31L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 15, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3104L, "seed-0031", "{\"level\":1}", "MessageCompleted", 31L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 15, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3201L, "seed-0032", "{}", "MessageImported", 32L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 16, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3202L, "seed-0032", "{}", "MessageAssigned", 32L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 16, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3203L, "seed-0032", "{\"level\":1}", "ReviewStarted", 32L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 16, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3204L, "seed-0032", "{\"level\":1}", "ReviewApproved", 32L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 16, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3301L, "seed-0033", "{}", "MessageImported", 33L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3401L, "seed-0034", "{}", "MessageImported", 34L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 18, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3402L, "seed-0034", "{}", "MessageAssigned", 34L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 18, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3403L, "seed-0034", "{\"level\":1}", "ReviewStarted", 34L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 18, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3404L, "seed-0034", "{\"level\":1}", "ReviewApproved", 34L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 18, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3405L, "seed-0034", "{\"level\":2}", "ReviewStarted", 34L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 2, 18, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3501L, "seed-0035", "{}", "MessageImported", 35L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3502L, "seed-0035", "{}", "MessageAssigned", 35L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 19, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3503L, "seed-0035", "{\"level\":1}", "ReviewStarted", 35L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 19, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3504L, "seed-0035", "{\"level\":1}", "ReviewApproved", 35L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 19, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3505L, "seed-0035", "{\"level\":2}", "ReviewStarted", 35L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 2, 19, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3601L, "seed-0036", "{}", "MessageImported", 36L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3701L, "seed-0037", "{}", "MessageImported", 37L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3702L, "seed-0037", "{}", "MessageAssigned", 37L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 21, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3703L, "seed-0037", "{\"level\":1}", "ReviewStarted", 37L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 21, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3704L, "seed-0037", "{\"level\":1}", "ReviewApproved", 37L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 21, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3705L, "seed-0037", "{\"level\":2}", "ReviewStarted", 37L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 2, 21, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3801L, "seed-0038", "{}", "MessageImported", 38L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 22, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 3802L, "seed-0038", "{}", "MessageAssigned", 38L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 2, 22, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3803L, "seed-0038", "{\"level\":1}", "ReviewStarted", 38L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 2, 22, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3804L, "seed-0038", "{\"level\":1}", "ReviewApproved", 38L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 22, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 3805L, "seed-0038", "{\"level\":2}", "ReviewStarted", 38L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 2, 22, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 3901L, "seed-0039", "{}", "MessageImported", 39L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 2, 23, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4001L, "seed-0040", "{}", "MessageImported", 40L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4002L, "seed-0040", "{}", "MessageAssigned", 40L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4003L, "seed-0040", "{\"level\":1}", "ReviewStarted", 40L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 0, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4004L, "seed-0040", "{\"level\":1}", "ReviewApproved", 40L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 0, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4005L, "seed-0040", "{\"level\":2}", "ReviewStarted", 40L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 0, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4101L, "seed-0041", "{}", "MessageImported", 41L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4102L, "seed-0041", "{}", "MessageAssigned", 41L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 1, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4201L, "seed-0042", "{}", "MessageImported", 42L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4202L, "seed-0042", "{}", "MessageAssigned", 42L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4203L, "seed-0042", "{\"level\":1}", "ReviewStarted", 42L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4204L, "seed-0042", "{\"level\":1}", "ReviewApproved", 42L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4205L, "seed-0042", "{\"level\":2}", "ReviewStarted", 42L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4206L, "seed-0042", "{\"level\":2}", "MessageCompleted", 42L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4301L, "seed-0043", "{}", "MessageImported", 43L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4302L, "seed-0043", "{}", "MessageAssigned", 43L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 3, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4303L, "seed-0043", "{\"level\":1}", "ReviewStarted", 43L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 3, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4304L, "seed-0043", "{\"level\":1}", "ReviewApproved", 43L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 3, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4305L, "seed-0043", "{\"level\":2}", "ReviewStarted", 43L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 3, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4306L, "seed-0043", "{\"level\":2}", "ReviewApproved", 43L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 3, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4401L, "seed-0044", "{}", "MessageImported", 44L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4402L, "seed-0044", "{}", "MessageAssigned", 44L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 4, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4501L, "seed-0045", "{}", "MessageImported", 45L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 5, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4502L, "seed-0045", "{}", "MessageAssigned", 45L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4503L, "seed-0045", "{\"level\":1}", "ReviewStarted", 45L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4504L, "seed-0045", "{\"level\":1}", "ReviewApproved", 45L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4505L, "seed-0045", "{\"level\":2}", "ReviewStarted", 45L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4506L, "seed-0045", "{\"level\":2}", "MessageCompleted", 45L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4601L, "seed-0046", "{}", "MessageImported", 46L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4602L, "seed-0046", "{}", "MessageAssigned", 46L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 6, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4603L, "seed-0046", "{\"level\":1}", "ReviewStarted", 46L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 6, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4604L, "seed-0046", "{\"level\":1}", "ReviewApproved", 46L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 6, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4605L, "seed-0046", "{\"level\":2}", "ReviewStarted", 46L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 6, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4606L, "seed-0046", "{\"level\":2}", "ReviewApproved", 46L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 6, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4701L, "seed-0047", "{}", "MessageImported", 47L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 7, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4702L, "seed-0047", "{}", "MessageAssigned", 47L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 7, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4801L, "seed-0048", "{}", "MessageImported", 48L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4802L, "seed-0048", "{}", "MessageAssigned", 48L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4803L, "seed-0048", "{\"level\":1}", "ReviewStarted", 48L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4804L, "seed-0048", "{\"level\":1}", "ReviewApproved", 48L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 4805L, "seed-0048", "{\"level\":2}", "ReviewStarted", 48L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4806L, "seed-0048", "{\"level\":2}", "MessageCompleted", 48L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4901L, "seed-0049", "{}", "MessageImported", 49L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 4902L, "seed-0049", "{}", "MessageAssigned", 49L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 9, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 4903L, "seed-0049", "{\"level\":1}", "ReviewStarted", 49L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 9, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5001L, "seed-0050", "{}", "MessageImported", 50L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5101L, "seed-0051", "{}", "MessageImported", 51L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5102L, "seed-0051", "{}", "MessageAssigned", 51L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5103L, "seed-0051", "{\"level\":1}", "ReviewStarted", 51L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5104L, "seed-0051", "{\"level\":1}", "ReviewApproved", 51L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5105L, "seed-0051", "{\"level\":2}", "ReviewStarted", 51L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5106L, "seed-0051", "{\"level\":2}", "ReviewApproved", 51L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5107L, "seed-0051", "{\"level\":3}", "ReviewStarted", 51L, "ThirdReviewInProgress", "WaitingForThirdReview", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 5201L, "seed-0052", "{}", "MessageImported", 52L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5202L, "seed-0052", "{}", "MessageAssigned", 52L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 12, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5203L, "seed-0052", "{\"level\":1}", "ReviewStarted", 52L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 12, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5301L, "seed-0053", "{}", "MessageImported", 53L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5401L, "seed-0054", "{}", "MessageImported", 54L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5402L, "seed-0054", "{}", "MessageAssigned", 54L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5403L, "seed-0054", "{\"level\":1}", "ReviewStarted", 54L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5404L, "seed-0054", "{\"level\":1}", "ReviewApproved", 54L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5405L, "seed-0054", "{\"level\":2}", "ReviewStarted", 54L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5406L, "seed-0054", "{\"level\":2}", "ReviewApproved", 54L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5407L, "seed-0054", "{\"level\":3}", "ReviewStarted", 54L, "ThirdReviewInProgress", "WaitingForThirdReview", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 5501L, "seed-0055", "{}", "MessageImported", 55L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5502L, "seed-0055", "{}", "MessageAssigned", 55L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 15, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5503L, "seed-0055", "{\"level\":1}", "ReviewStarted", 55L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 15, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5601L, "seed-0056", "{}", "MessageImported", 56L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 16, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5701L, "seed-0057", "{}", "MessageImported", 57L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5702L, "seed-0057", "{}", "MessageAssigned", 57L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 17, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5703L, "seed-0057", "{\"level\":1}", "ReviewStarted", 57L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 17, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5704L, "seed-0057", "{\"level\":1}", "MessageCompleted", 57L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 17, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5801L, "seed-0058", "{}", "MessageImported", 58L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 18, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5802L, "seed-0058", "{}", "MessageAssigned", 58L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 18, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5901L, "seed-0059", "{}", "MessageImported", 59L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 5902L, "seed-0059", "{}", "MessageAssigned", 59L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5903L, "seed-0059", "{\"level\":1}", "ReviewStarted", 59L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5904L, "seed-0059", "{\"level\":1}", "ReviewApproved", 59L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 5905L, "seed-0059", "{\"level\":2}", "ReviewStarted", 59L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5906L, "seed-0059", "{\"level\":2}", "ReviewApproved", 59L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 5907L, "seed-0059", "{\"level\":3}", "ReviewStarted", 59L, "ThirdReviewInProgress", "WaitingForThirdReview", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 5908L, "seed-0059", "{\"level\":3}", "MessageCompleted", 59L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 6001L, "seed-0060", "{}", "MessageImported", 60L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 20, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6002L, "seed-0060", "{}", "MessageAssigned", 60L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 20, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6003L, "seed-0060", "{\"level\":1}", "ReviewStarted", 60L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 20, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6004L, "seed-0060", "{\"level\":1}", "MessageCompleted", 60L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 20, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6101L, "seed-0061", "{}", "MessageImported", 61L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6102L, "seed-0061", "{}", "MessageAssigned", 61L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 21, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6201L, "seed-0062", "{}", "MessageImported", 62L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6202L, "seed-0062", "{}", "MessageAssigned", 62L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6203L, "seed-0062", "{\"level\":1}", "ReviewStarted", 62L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6204L, "seed-0062", "{\"level\":1}", "ReviewApproved", 62L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6205L, "seed-0062", "{\"level\":2}", "ReviewStarted", 62L, "SecondReviewInProgress", "WaitingForSecondReview", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6206L, "seed-0062", "{\"level\":2}", "ReviewApproved", 62L, "WaitingForThirdReview", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6207L, "seed-0062", "{\"level\":3}", "ReviewStarted", 62L, "ThirdReviewInProgress", "WaitingForThirdReview", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 6208L, "seed-0062", "{\"level\":3}", "MessageCompleted", 62L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 6301L, "seed-0063", "{}", "MessageImported", 63L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 3, 23, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6302L, "seed-0063", "{}", "MessageAssigned", 63L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 3, 23, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6303L, "seed-0063", "{\"level\":1}", "ReviewStarted", 63L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 3, 23, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6304L, "seed-0063", "{\"level\":1}", "MessageCompleted", 63L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 23, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6401L, "seed-0064", "{}", "MessageImported", 64L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6402L, "seed-0064", "{}", "MessageAssigned", 64L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 4, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6501L, "seed-0065", "{}", "MessageImported", 65L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 1, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6601L, "seed-0066", "{}", "MessageImported", 66L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6602L, "seed-0066", "{}", "MessageAssigned", 66L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 4, 2, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6603L, "seed-0066", "{\"level\":1}", "ReviewStarted", 66L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 4, 2, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 6701L, "seed-0067", "{}", "MessageImported", 67L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6801L, "seed-0068", "{}", "MessageImported", 68L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 4, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6901L, "seed-0069", "{}", "MessageImported", 69L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 5, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 6902L, "seed-0069", "{}", "MessageAssigned", 69L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 4, 5, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 6903L, "seed-0069", "{\"level\":1}", "ReviewStarted", 69L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 4, 5, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 7001L, "seed-0070", "{}", "MessageImported", 70L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 6, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7101L, "seed-0071", "{}", "MessageImported", 71L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 7, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7201L, "seed-0072", "{}", "MessageImported", 72L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7202L, "seed-0072", "{}", "MessageAssigned", 72L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 4, 8, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 7203L, "seed-0072", "{\"level\":1}", "ReviewStarted", 72L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 4, 8, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 7301L, "seed-0073", "{}", "MessageImported", 73L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7302L, "seed-0073", "{}", "MessageAssigned", 73L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 4, 9, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 7401L, "seed-0074", "{}", "MessageImported", 74L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7402L, "seed-0074", "{}", "MessageAssigned", 74L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 4, 10, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 7403L, "seed-0074", "{\"level\":1}", "ReviewStarted", 74L, "FirstReviewInProgress", "Assigned", new DateTimeOffset(new DateTime(2026, 8, 4, 10, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 7404L, "seed-0074", "{\"level\":1}", "ReviewApproved", 74L, "WaitingForSecondReview", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 4, 10, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 7501L, "seed-0075", "{}", "MessageImported", 75L, "New", null, new DateTimeOffset(new DateTime(2026, 8, 4, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { 7502L, "seed-0075", "{}", "MessageAssigned", 75L, "Assigned", "New", new DateTimeOffset(new DateTime(2026, 8, 4, 11, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 900251L, "seed-0025", "{\"level\":1}", "ReviewApproved", 25L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 9, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900281L, "seed-0028", "{\"level\":1}", "ReviewApproved", 28L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 12, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900311L, "seed-0031", "{\"level\":1}", "ReviewApproved", 31L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 2, 15, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900422L, "seed-0042", "{\"level\":2}", "ReviewApproved", 42L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 24, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 900452L, "seed-0045", "{\"level\":2}", "ReviewApproved", 45L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 24, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 900482L, "seed-0048", "{\"level\":2}", "ReviewApproved", 48L, "Completed", "SecondReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 24, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6 },
                    { 900571L, "seed-0057", "{\"level\":1}", "ReviewApproved", 57L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 17, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900593L, "seed-0059", "{\"level\":3}", "ReviewApproved", 59L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 34, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 900601L, "seed-0060", "{\"level\":1}", "ReviewApproved", 60L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 20, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 },
                    { 900623L, "seed-0062", "{\"level\":3}", "ReviewApproved", 62L, "Completed", "ThirdReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 34, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4 },
                    { 900631L, "seed-0063", "{\"level\":1}", "ReviewApproved", 63L, "Completed", "FirstReviewInProgress", new DateTimeOffset(new DateTime(2026, 8, 3, 23, 14, 59, 999, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 5 }
                });

            migrationBuilder.InsertData(
                table: "MessageRawData",
                columns: new[] { "MessageId", "RawContent" },
                values: new object[,]
                {
                    { 1L, "{1:F01SEED0001}{2:I199SWIFTREVIEW}{4::20:REF-0001-}" },
                    { 2L, "{1:F01SEED0002}{2:I299SWIFTREVIEW}{4::20:REF-0002-}" },
                    { 3L, "{1:F01SEED0003}{2:I671SWIFTREVIEW}{4::20:REF-0003-}" },
                    { 4L, "{1:F01SEED0004}{2:I700SWIFTREVIEW}{4::20:REF-0004-}" },
                    { 5L, "{1:F01SEED0005}{2:I710SWIFTREVIEW}{4::20:REF-0005-}" },
                    { 6L, "{1:F01SEED0006}{2:I760SWIFTREVIEW}{4::20:REF-0006-}" },
                    { 7L, "{1:F01SEED0007}{2:I799SWIFTREVIEW}{4::20:REF-0007-}" },
                    { 8L, "{1:F01SEED0008}{2:I999SWIFTREVIEW}{4::20:REF-0008-}" },
                    { 9L, "{1:F01SEED0009}{2:I199SWIFTREVIEW}{4::20:REF-0009-}" },
                    { 10L, "{1:F01SEED0010}{2:I299SWIFTREVIEW}{4::20:REF-0010-}" },
                    { 11L, "{1:F01SEED0011}{2:I671SWIFTREVIEW}{4::20:REF-0011-}" },
                    { 12L, "{1:F01SEED0012}{2:I700SWIFTREVIEW}{4::20:REF-0012-}" },
                    { 13L, "{1:F01SEED0013}{2:I710SWIFTREVIEW}{4::20:REF-0013-}" },
                    { 14L, "{1:F01SEED0014}{2:I760SWIFTREVIEW}{4::20:REF-0014-}" },
                    { 15L, "{1:F01SEED0015}{2:I799SWIFTREVIEW}{4::20:REF-0015-}" },
                    { 16L, "{1:F01SEED0016}{2:I999SWIFTREVIEW}{4::20:REF-0016-}" },
                    { 17L, "{1:F01SEED0017}{2:I199SWIFTREVIEW}{4::20:REF-0017-}" },
                    { 18L, "{1:F01SEED0018}{2:I299SWIFTREVIEW}{4::20:REF-0018-}" },
                    { 19L, "{1:F01SEED0019}{2:I671SWIFTREVIEW}{4::20:REF-0019-}" },
                    { 20L, "{1:F01SEED0020}{2:I700SWIFTREVIEW}{4::20:REF-0020-}" },
                    { 21L, "{1:F01SEED0021}{2:I710SWIFTREVIEW}{4::20:REF-0021-}" },
                    { 22L, "{1:F01SEED0022}{2:I760SWIFTREVIEW}{4::20:REF-0022-}" },
                    { 23L, "{1:F01SEED0023}{2:I799SWIFTREVIEW}{4::20:REF-0023-}" },
                    { 24L, "{1:F01SEED0024}{2:I999SWIFTREVIEW}{4::20:REF-0024-}" },
                    { 25L, "{1:F01SEED0025}{2:I199SWIFTREVIEW}{4::20:REF-0025-}" },
                    { 26L, "{1:F01SEED0026}{2:I299SWIFTREVIEW}{4::20:REF-0026-}" },
                    { 27L, "{1:F01SEED0027}{2:I671SWIFTREVIEW}{4::20:REF-0027-}" },
                    { 28L, "{1:F01SEED0028}{2:I700SWIFTREVIEW}{4::20:REF-0028-}" },
                    { 29L, "{1:F01SEED0029}{2:I710SWIFTREVIEW}{4::20:REF-0029-}" },
                    { 30L, "{1:F01SEED0030}{2:I760SWIFTREVIEW}{4::20:REF-0030-}" },
                    { 31L, "{1:F01SEED0031}{2:I799SWIFTREVIEW}{4::20:REF-0031-}" },
                    { 32L, "{1:F01SEED0032}{2:I999SWIFTREVIEW}{4::20:REF-0032-}" },
                    { 33L, "{1:F01SEED0033}{2:I199SWIFTREVIEW}{4::20:REF-0033-}" },
                    { 34L, "{1:F01SEED0034}{2:I299SWIFTREVIEW}{4::20:REF-0034-}" },
                    { 35L, "{1:F01SEED0035}{2:I671SWIFTREVIEW}{4::20:REF-0035-}" },
                    { 36L, "{1:F01SEED0036}{2:I700SWIFTREVIEW}{4::20:REF-0036-}" },
                    { 37L, "{1:F01SEED0037}{2:I710SWIFTREVIEW}{4::20:REF-0037-}" },
                    { 38L, "{1:F01SEED0038}{2:I760SWIFTREVIEW}{4::20:REF-0038-}" },
                    { 39L, "{1:F01SEED0039}{2:I799SWIFTREVIEW}{4::20:REF-0039-}" },
                    { 40L, "{1:F01SEED0040}{2:I999SWIFTREVIEW}{4::20:REF-0040-}" },
                    { 41L, "{1:F01SEED0041}{2:I199SWIFTREVIEW}{4::20:REF-0041-}" },
                    { 42L, "{1:F01SEED0042}{2:I299SWIFTREVIEW}{4::20:REF-0042-}" },
                    { 43L, "{1:F01SEED0043}{2:I671SWIFTREVIEW}{4::20:REF-0043-}" },
                    { 44L, "{1:F01SEED0044}{2:I700SWIFTREVIEW}{4::20:REF-0044-}" },
                    { 45L, "{1:F01SEED0045}{2:I710SWIFTREVIEW}{4::20:REF-0045-}" },
                    { 46L, "{1:F01SEED0046}{2:I760SWIFTREVIEW}{4::20:REF-0046-}" },
                    { 47L, "{1:F01SEED0047}{2:I799SWIFTREVIEW}{4::20:REF-0047-}" },
                    { 48L, "{1:F01SEED0048}{2:I999SWIFTREVIEW}{4::20:REF-0048-}" },
                    { 49L, "{1:F01SEED0049}{2:I199SWIFTREVIEW}{4::20:REF-0049-}" },
                    { 50L, "{1:F01SEED0050}{2:I299SWIFTREVIEW}{4::20:REF-0050-}" },
                    { 51L, "{1:F01SEED0051}{2:I671SWIFTREVIEW}{4::20:REF-0051-}" },
                    { 52L, "{1:F01SEED0052}{2:I700SWIFTREVIEW}{4::20:REF-0052-}" },
                    { 53L, "{1:F01SEED0053}{2:I710SWIFTREVIEW}{4::20:REF-0053-}" },
                    { 54L, "{1:F01SEED0054}{2:I760SWIFTREVIEW}{4::20:REF-0054-}" },
                    { 55L, "{1:F01SEED0055}{2:I799SWIFTREVIEW}{4::20:REF-0055-}" },
                    { 56L, "{1:F01SEED0056}{2:I999SWIFTREVIEW}{4::20:REF-0056-}" },
                    { 57L, "{1:F01SEED0057}{2:I199SWIFTREVIEW}{4::20:REF-0057-}" },
                    { 58L, "{1:F01SEED0058}{2:I299SWIFTREVIEW}{4::20:REF-0058-}" },
                    { 59L, "{1:F01SEED0059}{2:I671SWIFTREVIEW}{4::20:REF-0059-}" },
                    { 60L, "{1:F01SEED0060}{2:I700SWIFTREVIEW}{4::20:REF-0060-}" },
                    { 61L, "{1:F01SEED0061}{2:I710SWIFTREVIEW}{4::20:REF-0061-}" },
                    { 62L, "{1:F01SEED0062}{2:I760SWIFTREVIEW}{4::20:REF-0062-}" },
                    { 63L, "{1:F01SEED0063}{2:I799SWIFTREVIEW}{4::20:REF-0063-}" },
                    { 64L, "{1:F01SEED0064}{2:I999SWIFTREVIEW}{4::20:REF-0064-}" },
                    { 65L, "{1:F01SEED0065}{2:I199SWIFTREVIEW}{4::20:REF-0065-}" },
                    { 66L, "{1:F01SEED0066}{2:I299SWIFTREVIEW}{4::20:REF-0066-}" },
                    { 67L, "{1:F01SEED0067}{2:I671SWIFTREVIEW}{4::20:REF-0067-}" },
                    { 68L, "{1:F01SEED0068}{2:I700SWIFTREVIEW}{4::20:REF-0068-}" },
                    { 69L, "{1:F01SEED0069}{2:I710SWIFTREVIEW}{4::20:REF-0069-}" },
                    { 70L, "{1:F01SEED0070}{2:I760SWIFTREVIEW}{4::20:REF-0070-}" },
                    { 71L, "{1:F01SEED0071}{2:I799SWIFTREVIEW}{4::20:REF-0071-}" },
                    { 72L, "{1:F01SEED0072}{2:I999SWIFTREVIEW}{4::20:REF-0072-}" },
                    { 73L, "{1:F01SEED0073}{2:I199SWIFTREVIEW}{4::20:REF-0073-}" },
                    { 74L, "{1:F01SEED0074}{2:I299SWIFTREVIEW}{4::20:REF-0074-}" },
                    { 75L, "{1:F01SEED0075}{2:I671SWIFTREVIEW}{4::20:REF-0075-}" }
                });

            migrationBuilder.InsertData(
                table: "Reviews",
                columns: new[] { "Id", "Comment", "CompletedAt", "Level", "MessageId", "ReviewerId", "StartedAt", "Status" },
                values: new object[,]
                {
                    { 171L, null, null, 1, 17L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 1, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 181L, null, null, 1, 18L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 2, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 191L, null, null, 1, 19L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 3, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 201L, null, null, 1, 20L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 4, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 211L, null, null, 1, 21L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 5, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 221L, null, null, 1, 22L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 6, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 231L, null, null, 1, 23L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 7, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 241L, null, null, 1, 24L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 8, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 251L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 9, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 25L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 9, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 261L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 10, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 26L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 10, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 271L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 11, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 27L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 11, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 281L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 12, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 28L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 12, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 291L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 13, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 29L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 13, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 301L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 14, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 30L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 14, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 311L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 15, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 31L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 15, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 321L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 16, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 32L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 16, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 341L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 18, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 34L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 18, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 342L, null, null, 2, 34L, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 18, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 351L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 19, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 35L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 19, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 352L, null, null, 2, 35L, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 19, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 371L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 21, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 37L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 21, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 372L, null, null, 2, 37L, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 21, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 381L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 2, 22, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 38L, 5, new DateTimeOffset(new DateTime(2026, 8, 2, 22, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 382L, null, null, 2, 38L, 6, new DateTimeOffset(new DateTime(2026, 8, 2, 22, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 401L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 0, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 40L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 0, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 402L, null, null, 2, 40L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 0, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 421L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 42L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 2, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 422L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 2, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 42L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 2, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 431L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 3, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 43L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 3, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 432L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 3, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 43L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 3, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 451L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 45L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 5, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 452L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 5, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 45L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 5, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 461L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 6, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 46L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 6, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 462L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 6, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 46L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 6, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 481L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 48L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 8, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 482L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 8, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 48L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 8, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 491L, null, null, 1, 49L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 9, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 511L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 51L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 11, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 512L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 11, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 51L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 11, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 513L, null, null, 3, 51L, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 11, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 521L, null, null, 1, 52L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 12, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 541L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 54L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 14, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 542L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 14, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 54L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 14, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 543L, null, null, 3, 54L, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 551L, null, null, 1, 55L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 15, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 571L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 17, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 57L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 17, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 591L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 59L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 592L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 59L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 593L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 19, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, 59L, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 19, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 601L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 20, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 60L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 20, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 621L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 62L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 622L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 25, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 62L, 6, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 20, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 623L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 22, 35, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, 62L, 4, new DateTimeOffset(new DateTime(2026, 8, 3, 22, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 631L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 3, 23, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 63L, 5, new DateTimeOffset(new DateTime(2026, 8, 3, 23, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { 661L, null, null, 1, 66L, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 2, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 691L, null, null, 1, 69L, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 5, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 721L, null, null, 1, 72L, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 8, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "InProgress" },
                    { 741L, "Seed approval", new DateTimeOffset(new DateTime(2026, 8, 4, 10, 15, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 74L, 5, new DateTimeOffset(new DateTime(2026, 8, 4, 10, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignedBy",
                table: "Assignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignedTo",
                table: "Assignments",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MessageId",
                table: "Assignments",
                column: "MessageId",
                unique: true,
                filter: "[EndedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_MessageId_Timestamp",
                table: "AuditEvents",
                columns: new[] { "MessageId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_UserId",
                table: "AuditEvents",
                column: "UserId");

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
                name: "IX_Messages_CurrentAssigneeId",
                table: "Messages",
                column: "CurrentAssigneeId");

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
                name: "IX_Messages_WorkflowDefinitionId",
                table: "Messages",
                column: "WorkflowDefinitionId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MessageId_Level",
                table: "Reviews",
                columns: new[] { "MessageId", "Level" },
                unique: true,
                filter: "[Status] <> N'Undone'");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId",
                table: "Reviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_BranchId",
                table: "UserBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_DepartmentId",
                table: "UserDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_BranchId",
                table: "WorkflowDefinitions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_DepartmentId",
                table: "WorkflowDefinitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_MessageType_DepartmentId_BranchId",
                table: "WorkflowDefinitions",
                columns: new[] { "MessageType", "DepartmentId", "BranchId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowDefinitionId_Order",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowDefinitionId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assignments");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "MessageRawData");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserBranches");

            migrationBuilder.DropTable(
                name: "UserDepartments");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "WorkflowSteps");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
