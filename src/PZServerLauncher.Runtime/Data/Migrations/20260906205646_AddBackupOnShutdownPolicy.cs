using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PZServerLauncher.Runtime.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupOnShutdownPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BackupOnShutdownEnabled",
                table: "ServerProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShutdownBackupRetentionCount",
                table: "ServerProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackupOnShutdownEnabled",
                table: "ServerProfiles");

            migrationBuilder.DropColumn(
                name: "ShutdownBackupRetentionCount",
                table: "ServerProfiles");
        }
    }
}
