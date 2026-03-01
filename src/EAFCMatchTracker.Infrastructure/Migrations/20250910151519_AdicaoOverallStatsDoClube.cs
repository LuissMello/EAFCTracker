using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AdicaoOverallStatsDoClube : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OverallStats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    BestDivision = table.Column<string>(type: "text", nullable: true),
                    BestFinishGroup = table.Column<string>(type: "text", nullable: true),
                    GamesPlayed = table.Column<string>(type: "text", nullable: true),
                    GamesPlayedPlayoff = table.Column<string>(type: "text", nullable: true),
                    Goals = table.Column<string>(type: "text", nullable: true),
                    GoalsAgainst = table.Column<string>(type: "text", nullable: true),
                    Promotions = table.Column<string>(type: "text", nullable: true),
                    Relegations = table.Column<string>(type: "text", nullable: true),
                    Losses = table.Column<string>(type: "text", nullable: true),
                    Ties = table.Column<string>(type: "text", nullable: true),
                    Wins = table.Column<string>(type: "text", nullable: true),
                    Wstreak = table.Column<string>(type: "text", nullable: true),
                    Unbeatenstreak = table.Column<string>(type: "text", nullable: true),
                    SkillRating = table.Column<string>(type: "text", nullable: true),
                    Reputationtier = table.Column<string>(type: "text", nullable: true),
                    LeagueAppearances = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverallStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OverallStats_ClubId",
                table: "OverallStats",
                column: "ClubId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OverallStats");
        }
    }
}
