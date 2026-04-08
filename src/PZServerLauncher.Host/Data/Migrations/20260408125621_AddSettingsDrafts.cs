using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PZServerLauncher.Host.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettingsDrafts",
                columns: table => new
                {
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Branch = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CatalogVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PageId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourceSha256 = table.Column<string>(type: "TEXT", nullable: true),
                    IsDirty = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingsDrafts", x => new { x.ProfileId, x.Branch, x.CatalogId, x.CatalogVersion, x.PageId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettingsDrafts_UpdatedAtUtc",
                table: "SettingsDrafts",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettingsDrafts");
        }
    }
}
