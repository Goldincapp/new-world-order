using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class OperationGuideSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Three Operations were inserted into the guide. Keep every existing player on the chapter
            // they had already reached; accounts created after this migration begin at the new step zero.
            migrationBuilder.Sql("""
                UPDATE Players
                SET GuideStep = CASE GuideStep
                    WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 4 WHEN 3 THEN 5
                    WHEN 4 THEN 6 WHEN 5 THEN 7 WHEN 6 THEN 8 WHEN 7 THEN 10
                    WHEN 8 THEN 11 WHEN 9 THEN 12 WHEN 10 THEN 13 ELSE 14 END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Players
                SET GuideStep = CASE GuideStep
                    WHEN 0 THEN 0 WHEN 1 THEN 0 WHEN 2 THEN 1 WHEN 3 THEN 2
                    WHEN 4 THEN 2 WHEN 5 THEN 3 WHEN 6 THEN 4 WHEN 7 THEN 5
                    WHEN 8 THEN 6 WHEN 9 THEN 7 WHEN 10 THEN 7 WHEN 11 THEN 8
                    WHEN 12 THEN 9 WHEN 13 THEN 10 ELSE 11 END
                """);
        }
    }
}
