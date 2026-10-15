using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class WallLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WallLevel",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // "Reinforce the gate" is a new tutorial step 4 (index 3), right after Last Stand. Players who had already
            // beaten Last Stand move along one step with the rest of the tutorial, so they skip it instead of repeating it.
            migrationBuilder.Sql("UPDATE Players SET TutorialStep = TutorialStep + 1 WHERE TutorialStep >= 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Players SET TutorialStep = TutorialStep - 1 WHERE TutorialStep >= 4");

            migrationBuilder.DropColumn(
                name: "WallLevel",
                table: "Players");
        }
    }
}
