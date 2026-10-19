using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations;

[DbContext(typeof(GameDb))]
[Migration("20261019120000_ConstructionTimers")]
public partial class ConstructionTimers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "ConstructionEndsAt", table: "HomeTiles", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>(name: "PendingLevel", table: "HomeTiles", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "HqUpgradeEndsAt", table: "Players", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>(name: "PendingHqLevel", table: "Players", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PendingBuilding", table: "Parcels", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "BuildingEndsAt", table: "Parcels", type: "TEXT", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ConstructionEndsAt", table: "HomeTiles");
        migrationBuilder.DropColumn(name: "PendingLevel", table: "HomeTiles");
        migrationBuilder.DropColumn(name: "HqUpgradeEndsAt", table: "Players");
        migrationBuilder.DropColumn(name: "PendingHqLevel", table: "Players");
        migrationBuilder.DropColumn(name: "PendingBuilding", table: "Parcels");
        migrationBuilder.DropColumn(name: "BuildingEndsAt", table: "Parcels");
    }
}
