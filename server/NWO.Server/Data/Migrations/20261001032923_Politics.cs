using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Politics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ballots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ElectionNo = table.Column<int>(type: "INTEGER", nullable: false),
                    VoterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CandidateId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ballots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ElectionNo = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Speech = table.Column<string>(type: "TEXT", nullable: false),
                    SpeechAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Laws",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    ProposedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    For = table.Column<int>(type: "INTEGER", nullable: false),
                    Against = table.Column<int>(type: "INTEGER", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Laws", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LawVotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LawId = table.Column<long>(type: "INTEGER", nullable: false),
                    VoterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    For = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawVotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Government = table.Column<string>(type: "TEXT", nullable: false),
                    PresidentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PresidentName = table.Column<string>(type: "TEXT", nullable: true),
                    Legitimacy = table.Column<int>(type: "INTEGER", nullable: false),
                    Treasury = table.Column<double>(type: "REAL", nullable: false),
                    TaxRate = table.Column<double>(type: "REAL", nullable: false),
                    MarketTax = table.Column<double>(type: "REAL", nullable: false),
                    SmuggleFine = table.Column<double>(type: "REAL", nullable: false),
                    ElectionNo = table.Column<int>(type: "INTEGER", nullable: false),
                    ElectionAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSpeechAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ballots_NationId_ElectionNo_VoterId",
                table: "Ballots",
                columns: new[] { "NationId", "ElectionNo", "VoterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_NationId_ElectionNo_PlayerId",
                table: "Candidates",
                columns: new[] { "NationId", "ElectionNo", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Laws_NationId_Status",
                table: "Laws",
                columns: new[] { "NationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LawVotes_LawId_VoterId",
                table: "LawVotes",
                columns: new[] { "LawId", "VoterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ballots");

            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropTable(
                name: "Laws");

            migrationBuilder.DropTable(
                name: "LawVotes");

            migrationBuilder.DropTable(
                name: "Nations");
        }
    }
}
