using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PZServerLauncher.Host.Data.Migrations
{
    public partial class AddNamedWorkshopPresets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NamedWorkshopPresets",
                columns: table => new
                {
                    PresetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Branch = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    WorkshopItemIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EnabledModIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MapFoldersJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamedWorkshopPresets", x => x.PresetId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NamedWorkshopPresets_ProfileId_NormalizedName",
                table: "NamedWorkshopPresets",
                columns: new[] { "ProfileId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NamedWorkshopPresets_UpdatedAtUtc",
                table: "NamedWorkshopPresets",
                column: "UpdatedAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NamedWorkshopPresets");
        }
    }
}
