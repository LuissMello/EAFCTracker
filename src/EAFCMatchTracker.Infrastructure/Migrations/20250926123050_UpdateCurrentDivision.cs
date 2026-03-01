using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCurrentDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Details_CurrentDivision",
                table: "MatchClubs");

            migrationBuilder.AddColumn<int>(
                name: "CurrentDivision",
                table: "OverallStats",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentDivision",
                table: "OverallStats");

            migrationBuilder.AddColumn<int>(
                name: "Details_CurrentDivision",
                table: "MatchClubs",
                type: "integer",
                nullable: true);
        }
    }
}
