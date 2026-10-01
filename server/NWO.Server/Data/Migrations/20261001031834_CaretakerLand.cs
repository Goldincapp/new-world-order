using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaretakerLand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CampAssaultAt",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CampAssaultSector",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCampClearAt",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Camps",
                columns: table => new
                {
                    Sector = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClearedUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClearedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Camps", x => x.Sector);
                });

            migrationBuilder.CreateTable(
                name: "Drones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sector = table.Column<int>(type: "INTEGER", nullable: false),
                    Idx = table.Column<int>(type: "INTEGER", nullable: false),
                    DownUntil = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drones_Sector_Idx",
                table: "Drones",
                columns: new[] { "Sector", "Idx" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Camps");

            migrationBuilder.DropTable(
                name: "Drones");

            migrationBuilder.DropColumn(
                name: "CampAssaultAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CampAssaultSector",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastCampClearAt",
                table: "Players");
        }
    }
}
