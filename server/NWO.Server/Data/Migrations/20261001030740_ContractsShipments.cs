using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContractsShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Catches",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InspectDay",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectFadeAt",
                table: "Players",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "InspectStreak",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InspectsToday",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Misses",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sector = table.Column<int>(type: "INTEGER", nullable: false),
                    Resource = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<double>(type: "REAL", nullable: false),
                    Reward = table.Column<double>(type: "REAL", nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TakenById = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: false),
                    ContractId = table.Column<long>(type: "INTEGER", nullable: true),
                    Resource = table.Column<string>(type: "TEXT", nullable: false),
                    Qty = table.Column<double>(type: "REAL", nullable: false),
                    From = table.Column<int>(type: "INTEGER", nullable: false),
                    To = table.Column<int>(type: "INTEGER", nullable: false),
                    Legal = table.Column<bool>(type: "INTEGER", nullable: false),
                    Envelope = table.Column<double>(type: "REAL", nullable: false),
                    Reward = table.Column<double>(type: "REAL", nullable: false),
                    DepartAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArriveAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CheckAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Checked = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StopResolveAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StoppedBy = table.Column<string>(type: "TEXT", nullable: true),
                    HeldById = table.Column<Guid>(type: "TEXT", nullable: true),
                    HeldUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AuditPlayerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AuditAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InspectedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status",
                table: "Contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Status",
                table: "Shipments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropColumn(
                name: "Catches",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "InspectDay",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "InspectFadeAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "InspectStreak",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "InspectsToday",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Misses",
                table: "Players");
        }
    }
}
