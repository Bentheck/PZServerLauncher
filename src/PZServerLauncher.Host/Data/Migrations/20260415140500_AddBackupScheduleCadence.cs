using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PZServerLauncher.Host.Data;

#nullable disable

namespace PZServerLauncher.Host.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260415140500_AddBackupScheduleCadence")]
    public partial class AddBackupScheduleCadence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduledBackupIntervalHours",
                table: "ServerProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<string>(
                name: "ScheduledBackupStartLocalTime",
                table: "ServerProfiles",
                type: "TEXT",
                maxLength: 5,
                nullable: false,
                defaultValue: "03:00");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduledBackupIntervalHours",
                table: "ServerProfiles");

            migrationBuilder.DropColumn(
                name: "ScheduledBackupStartLocalTime",
                table: "ServerProfiles");
        }
    }
}
