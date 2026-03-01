using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class CreateGoalLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "PreAssists",
                table: "MatchPlayers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "MatchGoalLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<long>(type: "bigint", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    ScorerPlayerEntityId = table.Column<long>(type: "bigint", nullable: false),
                    AssistPlayerEntityId = table.Column<long>(type: "bigint", nullable: true),
                    PreAssistPlayerEntityId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchGoalLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchGoalLinks_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchGoalLinks_Players_AssistPlayerEntityId",
                        column: x => x.AssistPlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchGoalLinks_Players_PreAssistPlayerEntityId",
                        column: x => x.PreAssistPlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchGoalLinks_Players_ScorerPlayerEntityId",
                        column: x => x.ScorerPlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchGoalLinks_AssistPlayerEntityId",
                table: "MatchGoalLinks",
                column: "AssistPlayerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchGoalLinks_MatchId_ClubId",
                table: "MatchGoalLinks",
                columns: new[] { "MatchId", "ClubId" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchGoalLinks_PreAssistPlayerEntityId",
                table: "MatchGoalLinks",
                column: "PreAssistPlayerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchGoalLinks_ScorerPlayerEntityId",
                table: "MatchGoalLinks",
                column: "ScorerPlayerEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchGoalLinks");

            migrationBuilder.DropColumn(
                name: "PreAssists",
                table: "MatchPlayers");
        }
    }
}
