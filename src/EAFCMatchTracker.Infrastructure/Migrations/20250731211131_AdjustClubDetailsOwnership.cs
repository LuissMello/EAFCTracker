using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AdjustClubDetailsOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchClubs",
                table: "MatchClubs");

            migrationBuilder.DropIndex(
                name: "IX_MatchClubs_MatchId",
                table: "MatchClubs");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "MatchClubs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "Details_ClubId",
                table: "MatchClubs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Details_CrestAssetId",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_CrestColor",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_CustomAwayKitId",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_CustomKeeperKitId",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_CustomKitId",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_CustomThirdKitId",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_DCustomKit",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitAColor1",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitAColor2",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitAColor3",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitAColor4",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitColor1",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitColor2",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitColor3",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitColor4",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitId",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitThrdColor1",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitThrdColor2",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitThrdColor3",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_KitThrdColor4",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details_Name",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Details_RegionId",
                table: "MatchClubs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Details_StadName",
                table: "MatchClubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Details_TeamId",
                table: "MatchClubs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchClubs",
                table: "MatchClubs",
                columns: new[] { "MatchId", "ClubId" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerId_ClubId",
                table: "Players",
                columns: new[] { "PlayerId", "ClubId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerId_ClubId",
                table: "Players");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchClubs",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_ClubId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_CrestAssetId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_CrestColor",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_CustomAwayKitId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_CustomKeeperKitId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_CustomKitId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_CustomThirdKitId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_DCustomKit",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitAColor1",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitAColor2",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitAColor3",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitAColor4",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitColor1",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitColor2",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitColor3",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitColor4",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitThrdColor1",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitThrdColor2",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitThrdColor3",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_KitThrdColor4",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_Name",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_RegionId",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_StadName",
                table: "MatchClubs");

            migrationBuilder.DropColumn(
                name: "Details_TeamId",
                table: "MatchClubs");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "MatchClubs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchClubs",
                table: "MatchClubs",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ClubDetails",
                columns: table => new
                {
                    MatchClubEntityId = table.Column<long>(type: "bigint", nullable: false),
                    ClubId = table.Column<long>(type: "bigint", nullable: false),
                    CrestAssetId = table.Column<string>(type: "text", nullable: true),
                    CrestColor = table.Column<string>(type: "text", nullable: true),
                    CustomAwayKitId = table.Column<string>(type: "text", nullable: true),
                    CustomKeeperKitId = table.Column<string>(type: "text", nullable: true),
                    CustomKitId = table.Column<string>(type: "text", nullable: true),
                    CustomThirdKitId = table.Column<string>(type: "text", nullable: true),
                    DCustomKit = table.Column<string>(type: "text", nullable: true),
                    KitAColor1 = table.Column<string>(type: "text", nullable: true),
                    KitAColor2 = table.Column<string>(type: "text", nullable: true),
                    KitAColor3 = table.Column<string>(type: "text", nullable: true),
                    KitAColor4 = table.Column<string>(type: "text", nullable: true),
                    KitColor1 = table.Column<string>(type: "text", nullable: true),
                    KitColor2 = table.Column<string>(type: "text", nullable: true),
                    KitColor3 = table.Column<string>(type: "text", nullable: true),
                    KitColor4 = table.Column<string>(type: "text", nullable: true),
                    KitId = table.Column<string>(type: "text", nullable: true),
                    KitThrdColor1 = table.Column<string>(type: "text", nullable: true),
                    KitThrdColor2 = table.Column<string>(type: "text", nullable: true),
                    KitThrdColor3 = table.Column<string>(type: "text", nullable: true),
                    KitThrdColor4 = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    RegionId = table.Column<long>(type: "bigint", nullable: false),
                    StadName = table.Column<string>(type: "text", nullable: true),
                    TeamId = table.Column<long>(type: "bigint", nullable: false)
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
        }
    }
}
