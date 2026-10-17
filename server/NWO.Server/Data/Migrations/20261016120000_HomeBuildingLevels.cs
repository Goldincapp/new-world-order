using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations;

/// <summary>Adds independent progression to every building already placed at home.</summary>
[DbContext(typeof(GameDb))]
[Migration("20261016120000_HomeBuildingLevels")]
public class HomeBuildingLevels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Level",
            table: "HomeTiles",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Level", table: "HomeTiles");
    }
}
