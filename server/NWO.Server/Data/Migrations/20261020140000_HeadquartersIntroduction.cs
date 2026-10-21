using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    [DbContext(typeof(GameDb))]
    [Migration("20261020140000_HeadquartersIntroduction")]
    public partial class HeadquartersIntroduction : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Players SET TutorialStep = TutorialStep + 1 WHERE TutorialStep >= 1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Players SET TutorialStep = TutorialStep - 1 WHERE TutorialStep >= 2;");
        }
    }
}
