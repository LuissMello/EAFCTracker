using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AdicaoPlayoffAchievments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayoffAchievements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    SeasonId = table.Column<string>(type: "text", nullable: false),
                    SeasonName = table.Column<string>(type: "text", nullable: true),
                    BestDivision = table.Column<string>(type: "text", nullable: true),
                    BestFinishGroup = table.Column<string>(type: "text", nullable: true),
                    RetrievedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayoffAchievements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayoffAchievements_ClubId",
                table: "PlayoffAchievements",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayoffAchievements_ClubId_SeasonId",
                table: "PlayoffAchievements",
                columns: new[] { "ClubId", "SeasonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayoffAchievements");
        }
    }
}
