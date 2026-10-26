using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentDistributionLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO orp.Permissions (Name)
                SELECT p.Name FROM (VALUES (N'message.forward'), (N'distribution-list.manage')) p(Name)
                WHERE NOT EXISTS (SELECT 1 FROM orp.Permissions existing WHERE existing.Name = p.Name);
                INSERT INTO orp.RolePermissions (RoleId, PermissionId)
                SELECT r.Id, p.Id FROM orp.Roles r CROSS JOIN orp.Permissions p
                WHERE r.Name = N'Reviewer' AND p.Name IN (N'message.forward', N'distribution-list.manage')
                    AND NOT EXISTS (SELECT 1 FROM orp.RolePermissions rp
                                    WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
                """);
            migrationBuilder.CreateTable(
                name: "DistributionLists",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionLists_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "orp",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributionListEmails",
                schema: "orp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistributionListId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, collation: "Latin1_General_100_CI_AS_SC")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionListEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionListEmails_DistributionLists_DistributionListId",
                        column: x => x.DistributionListId,
                        principalSchema: "orp",
                        principalTable: "DistributionLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionListEmails_DistributionListId_Email",
                schema: "orp",
                table: "DistributionListEmails",
                columns: new[] { "DistributionListId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistributionLists_DepartmentId_Name",
                schema: "orp",
                table: "DistributionLists",
                columns: new[] { "DepartmentId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE rp FROM orp.RolePermissions rp
                INNER JOIN orp.Permissions p ON p.Id = rp.PermissionId
                WHERE p.Name IN (N'message.forward', N'distribution-list.manage');
                DELETE FROM orp.Permissions WHERE Name IN (N'message.forward', N'distribution-list.manage');
                """);
            migrationBuilder.DropTable(
                name: "DistributionListEmails",
                schema: "orp");

            migrationBuilder.DropTable(
                name: "DistributionLists",
                schema: "orp");
        }
    }
}
