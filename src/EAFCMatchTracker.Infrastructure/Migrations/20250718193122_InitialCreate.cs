using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    MatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeAgoId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    Playername = table.Column<string>(type: "text", nullable: false),
                    PlayerMatchStatsId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchClubs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    MatchId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GameNumber = table.Column<int>(type: "integer", nullable: false),
                    Goals = table.Column<short>(type: "smallint", nullable: false),
                    GoalsAgainst = table.Column<short>(type: "smallint", nullable: false),
                    Losses = table.Column<short>(type: "smallint", nullable: false),
                    MatchType = table.Column<short>(type: "smallint", nullable: false),
                    Result = table.Column<short>(type: "smallint", nullable: false),
                    Score = table.Column<short>(type: "smallint", nullable: false),
                    SeasonId = table.Column<short>(type: "smallint", nullable: false),
                    Team = table.Column<int>(type: "integer", nullable: false),
                    Ties = table.Column<short>(type: "smallint", nullable: false),
                    WinnerByDnf = table.Column<bool>(type: "boolean", nullable: false),
                    Wins = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchClubs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchClubs_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayers",
                columns: table => new
                {
                    MatchId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerEntityId = table.Column<long>(type: "bigint", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    Assists = table.Column<short>(type: "smallint", nullable: false),
                    Cleansheetsany = table.Column<short>(type: "smallint", nullable: false),
                    Cleansheetsdef = table.Column<short>(type: "smallint", nullable: false),
                    Cleansheetsgk = table.Column<short>(type: "smallint", nullable: false),
                    Goals = table.Column<short>(type: "smallint", nullable: false),
                    Goalsconceded = table.Column<short>(type: "smallint", nullable: false),
                    Losses = table.Column<short>(type: "smallint", nullable: false),
                    Mom = table.Column<bool>(type: "boolean", nullable: false),
                    Namespace = table.Column<short>(type: "smallint", nullable: false),
                    Passattempts = table.Column<short>(type: "smallint", nullable: false),
                    Passesmade = table.Column<short>(type: "smallint", nullable: false),
                    Pos = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<double>(type: "double precision", nullable: false),
                    Realtimegame = table.Column<string>(type: "text", nullable: false),
                    Realtimeidle = table.Column<string>(type: "text", nullable: false),
                    Redcards = table.Column<short>(type: "smallint", nullable: false),
                    Saves = table.Column<short>(type: "smallint", nullable: false),
                    Score = table.Column<short>(type: "smallint", nullable: false),
                    Shots = table.Column<short>(type: "smallint", nullable: false),
                    Tackleattempts = table.Column<short>(type: "smallint", nullable: false),
                    Tacklesmade = table.Column<short>(type: "smallint", nullable: false),
                    Vproattr = table.Column<string>(type: "text", nullable: false),
                    Vprohackreason = table.Column<string>(type: "text", nullable: false),
                    Wins = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayers", x => new { x.MatchId, x.PlayerEntityId });
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Players_PlayerEntityId",
                        column: x => x.PlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMatchStats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerEntityId = table.Column<long>(type: "bigint", nullable: false),
                    Aceleracao = table.Column<short>(type: "smallint", nullable: false),
                    Pique = table.Column<short>(type: "smallint", nullable: false),
                    Finalizacao = table.Column<short>(type: "smallint", nullable: false),
                    Falta = table.Column<short>(type: "smallint", nullable: false),
                    Cabeceio = table.Column<short>(type: "smallint", nullable: false),
                    ForcaDoChute = table.Column<short>(type: "smallint", nullable: false),
                    ChuteLonge = table.Column<short>(type: "smallint", nullable: false),
                    Voleio = table.Column<short>(type: "smallint", nullable: false),
                    Penalti = table.Column<short>(type: "smallint", nullable: false),
                    Visao = table.Column<short>(type: "smallint", nullable: false),
                    Cruzamento = table.Column<short>(type: "smallint", nullable: false),
                    Lancamento = table.Column<short>(type: "smallint", nullable: false),
                    PasseCurto = table.Column<short>(type: "smallint", nullable: false),
                    Curva = table.Column<short>(type: "smallint", nullable: false),
                    Agilidade = table.Column<short>(type: "smallint", nullable: false),
                    Equilibrio = table.Column<short>(type: "smallint", nullable: false),
                    PosAtaqueInutil = table.Column<short>(type: "smallint", nullable: false),
                    ControleBola = table.Column<short>(type: "smallint", nullable: false),
                    Conducao = table.Column<short>(type: "smallint", nullable: false),
                    Interceptacaos = table.Column<short>(type: "smallint", nullable: false),
                    NocaoDefensiva = table.Column<short>(type: "smallint", nullable: false),
                    DivididaEmPe = table.Column<short>(type: "smallint", nullable: false),
                    Carrinho = table.Column<short>(type: "smallint", nullable: false),
                    Impulsao = table.Column<short>(type: "smallint", nullable: false),
                    Folego = table.Column<short>(type: "smallint", nullable: false),
                    Forca = table.Column<short>(type: "smallint", nullable: false),
                    Reacao = table.Column<short>(type: "smallint", nullable: false),
                    Combatividade = table.Column<short>(type: "smallint", nullable: false),
                    Frieza = table.Column<short>(type: "smallint", nullable: false),
                    ElasticidadeGL = table.Column<short>(type: "smallint", nullable: false),
                    ManejoGL = table.Column<short>(type: "smallint", nullable: false),
                    ChuteGL = table.Column<short>(type: "smallint", nullable: false),
                    ReflexosGL = table.Column<short>(type: "smallint", nullable: false),
                    PosGL = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMatchStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerMatchStats_Players_PlayerEntityId",
                        column: x => x.PlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubDetails",
                columns: table => new
                {
                    MatchClubEntityId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    RegionId = table.Column<long>(type: "bigint", nullable: false),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    StadName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubDetails", x => x.MatchClubEntityId);
                    table.ForeignKey(
                        name: "FK_ClubDetails_MatchClubs_MatchClubEntityId",
                        column: x => x.MatchClubEntityId,
                        principalTable: "MatchClubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchClubs_MatchId",
                table: "MatchClubs",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_PlayerEntityId",
                table: "MatchPlayers",
                column: "PlayerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMatchStats_PlayerEntityId",
                table: "PlayerMatchStats",
                column: "PlayerEntityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubDetails");

            migrationBuilder.DropTable(
                name: "MatchPlayers");

            migrationBuilder.DropTable(
                name: "PlayerMatchStats");

            migrationBuilder.DropTable(
                name: "MatchClubs");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
