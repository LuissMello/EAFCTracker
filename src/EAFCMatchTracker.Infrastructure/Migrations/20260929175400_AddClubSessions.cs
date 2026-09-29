using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EAFCMatchTracker.Migrations;

[DbContext(typeof(EAFCContext))]
[Migration("20260929175400_AddClubSessions")]
public sealed class AddClubSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SessionGapMinutes", table: "TrackedClubs", type: "integer",
            nullable: false, defaultValue: 120);
        migrationBuilder.AddColumn<string>(
            name: "TimeZoneId", table: "TrackedClubs", type: "character varying(100)",
            maxLength: 100, nullable: false, defaultValue: "America/Sao_Paulo");
        migrationBuilder.CreateTable(
            name: "SessionBoundaries",
            columns: table => new
            {
                ClubId = table.Column<long>(type: "bigint", nullable: false),
                MatchId = table.Column<long>(type: "bigint", nullable: false),
                StartNewSession = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionBoundaries", x => new { x.ClubId, x.MatchId });
                table.ForeignKey("FK_SessionBoundaries_TrackedClubs_ClubId", x => x.ClubId,
                    "TrackedClubs", "ClubId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_SessionBoundaries_Matches_MatchId", x => x.MatchId,
                    "Matches", "MatchId", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(
            name: "IX_SessionBoundaries_MatchId", table: "SessionBoundaries", column: "MatchId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SessionBoundaries");
        migrationBuilder.DropColumn("SessionGapMinutes", "TrackedClubs");
        migrationBuilder.DropColumn("TimeZoneId", "TrackedClubs");
    }
}
