using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AdicaoNovosCamposFC26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "Archetypeid",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "BallDiveSaves",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "CrossSaves",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "GameTime",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "GoodDirectionSaves",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "MatchEventAggregate0",
                table: "MatchPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MatchEventAggregate1",
                table: "MatchPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MatchEventAggregate2",
                table: "MatchPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MatchEventAggregate3",
                table: "MatchPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "ParrySaves",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "PunchSaves",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "ReflexSaves",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SecondsPlayed",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "UserResult",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_MatchId",
                table: "Matches",
                column: "MatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_MatchId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Archetypeid",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "BallDiveSaves",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "CrossSaves",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "GameTime",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "GoodDirectionSaves",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "MatchEventAggregate0",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "MatchEventAggregate1",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "MatchEventAggregate2",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "MatchEventAggregate3",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "ParrySaves",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "PunchSaves",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "ReflexSaves",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "SecondsPlayed",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "UserResult",
                table: "MatchPlayers");
        }
    }
}
