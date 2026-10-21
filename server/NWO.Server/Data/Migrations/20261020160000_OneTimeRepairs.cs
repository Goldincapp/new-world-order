using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <summary>Repairs left by older builds, done once here rather than on every load: technologies kept by an old
    /// restart at the untouched research lesson (tutorial 8 steps, Field Guide step 6), and daily reward records which
    /// were written with their cash as a standing change.</summary>
    [DbContext(typeof(GameDb))]
    [Migration("20261020160000_OneTimeRepairs")]
    public partial class OneTimeRepairs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Players SET Techs = NULL WHERE TutorialStep >= 8 AND GuideStep = 6 AND ResearchId IS NULL AND Techs IS NOT NULL AND TRIM(Techs) <> '';");
            migrationBuilder.Sql("UPDATE Records SET Delta = 0 WHERE Kind = 'reward' AND Text LIKE 'Claimed day % login reward';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
