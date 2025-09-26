using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AdicaoDeNovosCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProHeight",
                table: "MatchPlayers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProName",
                table: "MatchPlayers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProOverall",
                table: "MatchPlayers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProOverallStr",
                table: "MatchPlayers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Details_CurrentDivision",
                table: "MatchClubs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProHeight",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "ProName",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "ProOverall",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "ProOverallStr",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "Details_CurrentDivision",
                table: "MatchClubs");
        }
    }
}
