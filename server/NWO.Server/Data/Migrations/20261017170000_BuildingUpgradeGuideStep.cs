using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    [DbContext(typeof(GameDb))]
    [Migration("20261017170000_BuildingUpgradeGuideStep")]
    public partial class BuildingUpgradeGuideStep : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The building-upgrade lesson was inserted before the old Market step.
            // Keep existing players on the same named chapter they had already reached.
            migrationBuilder.Sql("UPDATE Players SET GuideStep = GuideStep + 1 WHERE GuideStep >= 6");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Players SET GuideStep = GuideStep - 1 WHERE GuideStep >= 7");
        }
    }
}
