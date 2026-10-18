using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations;

[DbContext(typeof(GameDb))]
[Migration("20261018120000_RemoveCollectionTutorial")]
public partial class RemoveCollectionTutorial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "Xp", table: "Players", type: "INTEGER", nullable: false, defaultValue: 0);
        // Insert the new Stabilisation chapter before Expansion without changing the
        // chapter an existing tester is already working through.
        migrationBuilder.Sql("UPDATE Players SET GuideStep = GuideStep + 1 WHERE GuideStep >= 1");
        // Step 7 used to be a required collection lesson. Production is now collected
        // from visible world icons, so existing players at that boundary continue into
        // Expansion instead of falling back to the duplicated Last Stand guide step.
        migrationBuilder.Sql("UPDATE Players SET GuideStep = 1 WHERE TutorialStep >= 7 AND GuideStep = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE Players SET GuideStep = GuideStep - 1 WHERE GuideStep >= 2");
        migrationBuilder.DropColumn(name: "Xp", table: "Players");
    }
}
