using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaretakerPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectorPresence",
                columns: table => new
                {
                    Sector = table.Column<int>(type: "INTEGER", nullable: false),
                    Presence = table.Column<double>(type: "REAL", nullable: false),
                    Target = table.Column<double>(type: "REAL", nullable: false),
                    Concessions = table.Column<int>(type: "INTEGER", nullable: false),
                    PetitionConcessions = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPetitionAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAssaultAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorPresence", x => x.Sector);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorPresence");
        }
    }
}
