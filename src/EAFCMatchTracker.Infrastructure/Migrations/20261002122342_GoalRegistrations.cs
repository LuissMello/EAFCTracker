using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class GoalRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GoalRegistrationId",
                table: "MatchGoalLinks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GoalRegistrationId",
                table: "Matches",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoalRegistrations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    OpponentClubId = table.Column<long>(type: "bigint", nullable: false),
                    OpponentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MatchId = table.Column<long>(type: "bigint", nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalRegistrations_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GoalRegistrationGoals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoalRegistrationId = table.Column<long>(type: "bigint", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ScorerPlayerEntityId = table.Column<long>(type: "bigint", nullable: false),
                    AssistPlayerEntityId = table.Column<long>(type: "bigint", nullable: true),
                    PreAssistPlayerEntityId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalRegistrationGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalRegistrationGoals_GoalRegistrations_GoalRegistrationId",
                        column: x => x.GoalRegistrationId,
                        principalTable: "GoalRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalRegistrationGoals_Players_AssistPlayerEntityId",
                        column: x => x.AssistPlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalRegistrationGoals_Players_PreAssistPlayerEntityId",
                        column: x => x.PreAssistPlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalRegistrationGoals_Players_ScorerPlayerEntityId",
                        column: x => x.ScorerPlayerEntityId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchGoalLinks_GoalRegistrationId",
                table: "MatchGoalLinks",
                column: "GoalRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_GoalRegistrationId",
                table: "Matches",
                column: "GoalRegistrationId",
                unique: true,
                filter: "\"GoalRegistrationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRegistrationGoals_AssistPlayerEntityId",
                table: "GoalRegistrationGoals",
                column: "AssistPlayerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRegistrationGoals_GoalRegistrationId_Order",
                table: "GoalRegistrationGoals",
                columns: new[] { "GoalRegistrationId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalRegistrationGoals_PreAssistPlayerEntityId",
                table: "GoalRegistrationGoals",
                column: "PreAssistPlayerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRegistrationGoals_ScorerPlayerEntityId",
                table: "GoalRegistrationGoals",
                column: "ScorerPlayerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRegistrations_ClubId_OpponentClubId_Status",
                table: "GoalRegistrations",
                columns: new[] { "ClubId", "OpponentClubId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalRegistrations_MatchId",
                table: "GoalRegistrations",
                column: "MatchId",
                unique: true,
                filter: "\"MatchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRegistrations_Status_CreatedAt",
                table: "GoalRegistrations",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_GoalRegistrations_GoalRegistrationId",
                table: "Matches",
                column: "GoalRegistrationId",
                principalTable: "GoalRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchGoalLinks_GoalRegistrations_GoalRegistrationId",
                table: "MatchGoalLinks",
                column: "GoalRegistrationId",
                principalTable: "GoalRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_GoalRegistrations_GoalRegistrationId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchGoalLinks_GoalRegistrations_GoalRegistrationId",
                table: "MatchGoalLinks");

            migrationBuilder.DropTable(
                name: "GoalRegistrationGoals");

            migrationBuilder.DropTable(
                name: "GoalRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_MatchGoalLinks_GoalRegistrationId",
                table: "MatchGoalLinks");

            migrationBuilder.DropIndex(
                name: "IX_Matches_GoalRegistrationId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "GoalRegistrationId",
                table: "MatchGoalLinks");

            migrationBuilder.DropColumn(
                name: "GoalRegistrationId",
                table: "Matches");
        }
    }
}
