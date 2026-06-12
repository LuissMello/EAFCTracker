using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class OverallStatsHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OverallStats_ClubId",
                table: "OverallStats");

            migrationBuilder.AddColumn<long>(
                name: "MatchId",
                table: "OverallStats",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OverallStats_ClubId",
                table: "OverallStats",
                column: "ClubId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OverallStats_ClubId",
                table: "OverallStats");

            migrationBuilder.DropColumn(
                name: "MatchId",
                table: "OverallStats");

            migrationBuilder.CreateIndex(
                name: "IX_OverallStats_ClubId",
                table: "OverallStats",
                column: "ClubId",
                unique: true);
        }
    }
}
