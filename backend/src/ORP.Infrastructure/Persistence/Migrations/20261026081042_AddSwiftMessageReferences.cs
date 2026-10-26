using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSwiftMessageReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RelatedReference",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionReferenceNumber",
                schema: "orp",
                table: "SwiftMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedReference",
                schema: "orp",
                table: "SwiftMessages");

            migrationBuilder.DropColumn(
                name: "TransactionReferenceNumber",
                schema: "orp",
                table: "SwiftMessages");
        }
    }
}
