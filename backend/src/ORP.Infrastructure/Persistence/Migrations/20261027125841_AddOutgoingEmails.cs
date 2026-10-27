using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutgoingEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutgoingEmails",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    FromDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Importance = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutgoingEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutgoingEmails_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "orp",
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OutgoingEmails_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "orp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailAttachments",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutgoingEmailId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAttachments_OutgoingEmails_OutgoingEmailId",
                        column: x => x.OutgoingEmailId,
                        principalSchema: "orp",
                        principalTable: "OutgoingEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailRecipients",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutgoingEmailId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRecipients_OutgoingEmails_OutgoingEmailId",
                        column: x => x.OutgoingEmailId,
                        principalSchema: "orp",
                        principalTable: "OutgoingEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachments_OutgoingEmailId",
                schema: "orp",
                table: "EmailAttachments",
                column: "OutgoingEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_OutgoingEmailId",
                schema: "orp",
                table: "EmailRecipients",
                column: "OutgoingEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingEmails_CreatedByUserId",
                schema: "orp",
                table: "OutgoingEmails",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutgoingEmails_MessageId",
                schema: "orp",
                table: "OutgoingEmails",
                column: "MessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAttachments",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "EmailRecipients",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "OutgoingEmails",
                schema: "orp");
        }
    }
}
