using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class OutpostSiege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Contributors",
                table: "SectorPresence",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OutpostHq",
                table: "SectorPresence",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<double>(
                name: "OutpostT1",
                table: "SectorPresence",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<double>(
                name: "OutpostT2",
                table: "SectorPresence",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Contributors",
                table: "SectorPresence");

            migrationBuilder.DropColumn(
                name: "OutpostHq",
                table: "SectorPresence");

            migrationBuilder.DropColumn(
                name: "OutpostT1",
                table: "SectorPresence");

            migrationBuilder.DropColumn(
                name: "OutpostT2",
                table: "SectorPresence");
        }
    }
}
