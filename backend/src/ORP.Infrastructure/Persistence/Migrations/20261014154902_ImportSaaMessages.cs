using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportSaaMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous warehouse records cannot be identified by SAA file path/hash.
            migrationBuilder.Sql("DELETE FROM [orp].[AuditEvents]; DELETE FROM [orp].[Reviews]; DELETE FROM [orp].[Assignments]; DELETE FROM [orp].[Messages]; DELETE FROM [orp].[SwiftMessages];");

            migrationBuilder.DropForeignKey(
                name: "FK_SwiftMessages_Branches_BranchId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_SwiftMessages_Departments_DepartmentId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropIndex(
                name: "IX_SwiftMessages_BranchId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropIndex(
                name: "IX_SwiftMessages_DepartmentId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropIndex(
                name: "IX_SwiftMessages_WarehouseId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "BackendDirection",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "BodyContainsMt",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "BodyContainsMx",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "CounterParty",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "CounterPartyCountry",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "CreationDate",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "Json",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "LastModificationDate",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "LastSynchronizedAtUtc",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "MessageDate",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "MessageFormatVersion",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "MessageLength",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "MessageInputReference",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "MessageTypeShort",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "RoutingError",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "RoutingStatus",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "Service",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "SessionNumber",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "SourceInterface",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "StatusDate",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.RenameColumn(
                name: "SenderRequestor",
                schema: "orp",
                table: "SwiftMessages",
                newName: "Sender");

            migrationBuilder.RenameColumn(
                name: "ReceiverResponder",
                schema: "orp",
                table: "SwiftMessages",
                newName: "Receiver");

            migrationBuilder.AlterColumn<string>(
                name: "MessageType",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Branch",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                schema: "orp",
                table: "SwiftMessages",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                collation: "Latin1_General_100_CI_AS");

            migrationBuilder.AddColumn<string>(
                name: "Host",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Time",
                schema: "orp",
                table: "SwiftMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                schema: "orp",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                schema: "orp",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceivedAt",
                schema: "orp",
                table: "Messages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwiftMessages_FilePath",
                schema: "orp",
                table: "SwiftMessages",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_BranchId",
                schema: "orp",
                table: "Messages",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DepartmentId",
                schema: "orp",
                table: "Messages",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Branches_BranchId",
                schema: "orp",
                table: "Messages",
                column: "BranchId",
                principalSchema: "orp",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Departments_DepartmentId",
                schema: "orp",
                table: "Messages",
                column: "DepartmentId",
                principalSchema: "orp",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [orp].[AuditEvents]; DELETE FROM [orp].[Reviews]; DELETE FROM [orp].[Assignments]; DELETE FROM [orp].[Messages]; DELETE FROM [orp].[SwiftMessages];");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Branches_BranchId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Departments_DepartmentId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_SwiftMessages_FilePath",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_BranchId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_DepartmentId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "Branch",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "FilePath",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "Host",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "Time",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "orp",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                schema: "orp",
                table: "Messages");

            migrationBuilder.RenameColumn(
                name: "Sender",
                schema: "orp",
                table: "SwiftMessages",
                newName: "SenderRequestor");

            migrationBuilder.RenameColumn(
                name: "Receiver",
                schema: "orp",
                table: "SwiftMessages",
                newName: "ReceiverResponder");

            migrationBuilder.AlterColumn<string>(
                name: "MessageType",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "BackendDirection",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageInputReference",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BodyContainsMt",
                schema: "orp",
                table: "SwiftMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BodyContainsMx",
                schema: "orp",
                table: "SwiftMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                schema: "orp",
                table: "SwiftMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterParty",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterPartyCountry",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreationDate",
                schema: "orp",
                table: "SwiftMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                schema: "orp",
                table: "SwiftMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Json",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModificationDate",
                schema: "orp",
                table: "SwiftMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSynchronizedAtUtc",
                schema: "orp",
                table: "SwiftMessages",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MessageDate",
                schema: "orp",
                table: "SwiftMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageFormatVersion",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageLength",
                schema: "orp",
                table: "SwiftMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MessageTypeShort",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutingError",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutingStatus",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SequenceNumber",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Service",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionNumber",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceInterface",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusDate",
                schema: "orp",
                table: "SwiftMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseId",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddForeignKey(
                name: "FK_SwiftMessages_Branches_BranchId",
                schema: "orp",
                table: "SwiftMessages",
                column: "BranchId",
                principalSchema: "orp",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SwiftMessages_Departments_DepartmentId",
                schema: "orp",
                table: "SwiftMessages",
                column: "DepartmentId",
                principalSchema: "orp",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
