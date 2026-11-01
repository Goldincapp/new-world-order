using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations;

[DbContext(typeof(GameDb))]
[Migration("20261028090000_ExtractionCargoRewards")]
public partial class ExtractionCargoRewards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "ResearchCreditMinutes",
            table: "Players",
            type: "REAL",
            nullable: false,
            defaultValue: 0d);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ResearchCreditMinutes", table: "Players");
    }
}
