using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AtualizandoCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Number",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Matches");

            migrationBuilder.AddColumn<long>(
                name: "PlayerMatchStatsEntityId",
                table: "MatchPlayers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerMatchStatsEntityId",
                table: "MatchPlayers");

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Matches",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
