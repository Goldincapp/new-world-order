using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations;

[DbContext(typeof(GameDb))]
[Migration("20261020120000_TimberAndOreProgression")]
public partial class TimberAndOreProgression : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(name: "Timber", table: "Players", type: "REAL", nullable: false, defaultValue: 0.0);
        migrationBuilder.AddColumn<double>(name: "Ore", table: "Players", type: "REAL", nullable: false, defaultValue: 0.0);

        // Existing saves have already earned the materials represented by completed
        // lessons. Preserve their position while inserting the Mine and Oil lessons.
        migrationBuilder.Sql("""
            UPDATE Players
            SET Timber = CASE WHEN GuideStep >= 4 THEN 150 ELSE Timber END,
                Ore = CASE WHEN GuideStep >= 7 THEN 100 ELSE Ore END;

            UPDATE Players
            SET GuideStep = CASE
                WHEN GuideStep >= 8 THEN GuideStep + 2
                WHEN GuideStep = 7 THEN 8
                ELSE GuideStep
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE Players
            SET GuideStep = CASE
                WHEN GuideStep >= 10 THEN GuideStep - 2
                WHEN GuideStep = 8 THEN 7
                ELSE GuideStep
            END;
            """);
        migrationBuilder.DropColumn(name: "Timber", table: "Players");
        migrationBuilder.DropColumn(name: "Ore", table: "Players");
    }
}
