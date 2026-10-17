using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentRoutingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepartmentRoutingRules",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    MatchKind = table.Column<int>(type: "int", nullable: false),
                    MessageTypePattern = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SourceFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SourceSheet = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceRow = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentRoutingRules", x => x.Id);
                    table.CheckConstraint("CK_DepartmentRoutingRules_Direction", "[Direction] IN (1, 2)");
                    table.CheckConstraint("CK_DepartmentRoutingRules_MatchKind", "[MatchKind] IN (1, 2, 3)");
                    table.CheckConstraint("CK_DepartmentRoutingRules_SourceRow", "[SourceRow] > 0");
                    table.ForeignKey(
                        name: "FK_DepartmentRoutingRules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "orp",
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentRoutingRules_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRoutingRules_BranchId",
                schema: "orp",
                table: "DepartmentRoutingRules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRoutingRules_DepartmentId",
                schema: "orp",
                table: "DepartmentRoutingRules",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRoutingRules_Direction_BranchId_Priority",
                schema: "orp",
                table: "DepartmentRoutingRules",
                columns: new[] { "Direction", "BranchId", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentRoutingRules",
                schema: "orp");
        }
    }
}
