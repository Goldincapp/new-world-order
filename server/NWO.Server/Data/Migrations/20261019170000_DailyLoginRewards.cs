using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations;

[DbContext(typeof(GameDb))]
[Migration("20261019170000_DailyLoginRewards")]
public partial class DailyLoginRewards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "DailyRewardDay", table: "Players", type: "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(name: "LastDailyRewardAt", table: "Players", type: "TEXT", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DailyRewardDay", table: "Players");
        migrationBuilder.DropColumn(name: "LastDailyRewardAt", table: "Players");
    }
}
