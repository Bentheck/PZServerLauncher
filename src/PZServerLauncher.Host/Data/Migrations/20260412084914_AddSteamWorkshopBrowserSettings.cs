using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PZServerLauncher.Host.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSteamWorkshopBrowserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedSteamWebApiKey",
                table: "HostSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedSteamWebApiKey",
                table: "HostSettings");
        }
    }
}
