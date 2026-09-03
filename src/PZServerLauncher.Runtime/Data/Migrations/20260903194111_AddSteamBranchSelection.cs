using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PZServerLauncher.Runtime.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSteamBranchSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SteamBranch",
                table: "ServerProfiles",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "public");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SteamBranch",
                table: "ServerProfiles");
        }
    }
}
