using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class GameVersionsEIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameVersionId",
                table: "TrackedClubs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameVersionId",
                table: "PlayoffAchievements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameVersionId",
                table: "OverallStats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameVersionId",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameVersions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "GameVersions",
                columns: new[] { "Id", "IsCurrent", "Name", "StartsAt", "Version" },
                values: new object[,]
                {
                    { 1, false, "FC25", new DateTimeOffset(new DateTime(2024, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 25 },
                    { 2, false, "FC26", new DateTimeOffset(new DateTime(2025, 9, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 26 },
                    { 3, true, "FC27", null, 27 }
                });

            // ---------------------------------------------------------------------------------------------
            // BACKFILL (roda antes de criar índices/FKs; todos os UPDATEs são idempotentes: WHERE "GameVersionId" IS NULL)
            //
            // Premissas:
            //  - Clubes rastreados hoje são do FC26 (Version 26); nenhum clube do FC27 é rastreado ainda.
            //  - Todas as partidas e overall stats já gravados são anteriores ao FC27.
            //  - Matches."Timestamp" é timestamptz gravado em UTC (epoch seconds da EA -> UTC). Corte: 2025-09-26 00:00 UTC
            //    (lançamento do FC26): antes -> FC25 (Version 25), a partir daí -> FC26 (Version 26).
            //  - OverallStats: usa o horário da partida a que o registro pertence (MatchId); registros legados sem partida
            //    (MatchId nulo, ou partida já apagada) usam UpdatedAtUtc. Mesmo corte.
            //  - PlayoffAchievements NÃO recebem backfill (sem data confiável) e permanecem com GameVersionId NULL.
            // Os Ids são resolvidos por Version (não por Id fixo).
            // ---------------------------------------------------------------------------------------------
            migrationBuilder.Sql(@"
UPDATE ""TrackedClubs""
SET ""GameVersionId"" = (SELECT gv.""Id"" FROM ""GameVersions"" AS gv WHERE gv.""Version"" = 26)
WHERE ""GameVersionId"" IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE ""Matches""
SET ""GameVersionId"" = CASE
        WHEN ""Timestamp"" < TIMESTAMPTZ '2025-09-26 00:00:00+00'
            THEN (SELECT gv.""Id"" FROM ""GameVersions"" AS gv WHERE gv.""Version"" = 25)
        ELSE (SELECT gv.""Id"" FROM ""GameVersions"" AS gv WHERE gv.""Version"" = 26)
    END
WHERE ""GameVersionId"" IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE ""OverallStats"" AS o
SET ""GameVersionId"" = CASE
        WHEN COALESCE(
                (SELECT m.""Timestamp"" FROM ""Matches"" AS m WHERE m.""MatchId"" = o.""MatchId""),
                o.""UpdatedAtUtc"") < TIMESTAMPTZ '2025-09-26 00:00:00+00'
            THEN (SELECT gv.""Id"" FROM ""GameVersions"" AS gv WHERE gv.""Version"" = 25)
        ELSE (SELECT gv.""Id"" FROM ""GameVersions"" AS gv WHERE gv.""Version"" = 26)
    END
WHERE o.""GameVersionId"" IS NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedClubs_GameVersionId",
                table: "TrackedClubs",
                column: "GameVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayoffAchievements_GameVersionId",
                table: "PlayoffAchievements",
                column: "GameVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OverallStats_ClubId_UpdatedAtUtc",
                table: "OverallStats",
                columns: new[] { "ClubId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OverallStats_GameVersionId",
                table: "OverallStats",
                column: "GameVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_GameVersionId",
                table: "Matches",
                column: "GameVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Timestamp",
                table: "Matches",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_MatchClubs_ClubId_MatchId",
                table: "MatchClubs",
                columns: new[] { "ClubId", "MatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_IsCurrent",
                table: "GameVersions",
                column: "IsCurrent",
                unique: true,
                filter: "\"IsCurrent\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_Version",
                table: "GameVersions",
                column: "Version",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_GameVersions_GameVersionId",
                table: "Matches",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OverallStats_GameVersions_GameVersionId",
                table: "OverallStats",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayoffAchievements_GameVersions_GameVersionId",
                table: "PlayoffAchievements",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackedClubs_GameVersions_GameVersionId",
                table: "TrackedClubs",
                column: "GameVersionId",
                principalTable: "GameVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverte esquema e índices. Os valores de GameVersionId (backfill) somem junto com as colunas.
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_GameVersions_GameVersionId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_OverallStats_GameVersions_GameVersionId",
                table: "OverallStats");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayoffAchievements_GameVersions_GameVersionId",
                table: "PlayoffAchievements");

            migrationBuilder.DropForeignKey(
                name: "FK_TrackedClubs_GameVersions_GameVersionId",
                table: "TrackedClubs");

            migrationBuilder.DropTable(
                name: "GameVersions");

            migrationBuilder.DropIndex(
                name: "IX_TrackedClubs_GameVersionId",
                table: "TrackedClubs");

            migrationBuilder.DropIndex(
                name: "IX_PlayoffAchievements_GameVersionId",
                table: "PlayoffAchievements");

            migrationBuilder.DropIndex(
                name: "IX_OverallStats_ClubId_UpdatedAtUtc",
                table: "OverallStats");

            migrationBuilder.DropIndex(
                name: "IX_OverallStats_GameVersionId",
                table: "OverallStats");

            migrationBuilder.DropIndex(
                name: "IX_Matches_GameVersionId",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_Timestamp",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_MatchClubs_ClubId_MatchId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "TrackedClubs");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "PlayoffAchievements");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "OverallStats");

            migrationBuilder.DropColumn(
                name: "GameVersionId",
                table: "Matches");
        }
    }
}
