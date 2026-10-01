using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NWO.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecoveryCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecoveryHash",
                table: "Players",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryHash",
                table: "Players");
        }
    }
}
