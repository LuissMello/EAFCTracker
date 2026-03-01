using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAFCMatchTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddClubCustomKits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CrestAssetId",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CrestColor",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomAwayKitId",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomKeeperKitId",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomKitId",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomThirdKitId",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DCustomKit",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitAColor1",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitAColor2",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitAColor3",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitAColor4",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitColor1",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitColor2",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitColor3",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitColor4",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitId",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitThrdColor1",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitThrdColor2",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitThrdColor3",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KitThrdColor4",
                table: "ClubDetails",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CrestAssetId",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "CrestColor",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "CustomAwayKitId",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "CustomKeeperKitId",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "CustomKitId",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "CustomThirdKitId",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "DCustomKit",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitAColor1",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitAColor2",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitAColor3",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitAColor4",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitColor1",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitColor2",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitColor3",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitColor4",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitId",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitThrdColor1",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitThrdColor2",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitThrdColor3",
                table: "ClubDetails");

            migrationBuilder.DropColumn(
                name: "KitThrdColor4",
                table: "ClubDetails");
        }
    }
}
