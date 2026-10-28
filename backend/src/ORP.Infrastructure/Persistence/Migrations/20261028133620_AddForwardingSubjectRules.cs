using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForwardingSubjectRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForwardingSubjectRules",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Template = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForwardingSubjectRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForwardingSubjectRules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "orp",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForwardingSubjectRules_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForwardingSubjectRules_BranchId",
                schema: "orp",
                table: "ForwardingSubjectRules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardingSubjectRules_DepartmentId",
                schema: "orp",
                table: "ForwardingSubjectRules",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForwardingSubjectRules_Name",
                schema: "orp",
                table: "ForwardingSubjectRules",
                column: "Name",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO orp.ForwardingSubjectRules (Name, IsActive, Priority, MessageType, BranchId, DepartmentId, Template)
                SELECT N'TFO forwarding subject', 1, 100, NULL, NULL, Id, N'FW: {MessageType} - {Field21}'
                FROM orp.Departments WHERE Name = N'TFO';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForwardingSubjectRules",
                schema: "orp");
        }
    }
}
