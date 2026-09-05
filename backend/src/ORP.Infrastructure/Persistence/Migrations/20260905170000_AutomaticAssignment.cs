using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORP.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ORPDbContext))]
[Migration("20260905170000_AutomaticAssignment")]
public sealed class AutomaticAssignment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            schema: "ORP",
            table: "Messages",
            type: "rowversion",
            rowVersion: true,
            nullable: false,
            defaultValue: Array.Empty<byte>());

        migrationBuilder.AlterColumn<int>(
            name: "AssignedBy",
            schema: "ORP",
            table: "Assignments",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            schema: "ORP",
            table: "Messages");

        migrationBuilder.Sql(
            "UPDATE [ORP].[Assignments] SET [AssignedBy] = [AssignedTo] WHERE [AssignedBy] IS NULL;");
        migrationBuilder.AlterColumn<int>(
            name: "AssignedBy",
            schema: "ORP",
            table: "Assignments",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);
    }
}
