using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    [DbContext(typeof(GameDb))]
    [Migration("20261021120000_AllianceSignalBreach")]
    public partial class AllianceSignalBreach : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllianceEventScores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AllianceId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Cycle = table.Column<string>(type: "TEXT", nullable: false),
                    BestScore = table.Column<int>(type: "INTEGER", nullable: false),
                    PowerRating = table.Column<int>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    RewardTier = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_AllianceEventScores", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_AllianceEventScores_AllianceId_PlayerId_Cycle",
                table: "AllianceEventScores",
                columns: new[] { "AllianceId", "PlayerId", "Cycle" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "AllianceEventScores");
    }
}
