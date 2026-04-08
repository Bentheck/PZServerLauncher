using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PZServerLauncher.Host.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialHostSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: false),
                    ActorType = table.Column<string>(type: "TEXT", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", nullable: true),
                    Detail = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "HostSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LoopbackPort = table.Column<int>(type: "INTEGER", nullable: false),
                    StartHostWithWindows = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemoteAccessEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemoteBindAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RemoteHttpsPort = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicHostname = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CertificatePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreateFirewallRule = table.Column<bool>(type: "INTEGER", nullable: false),
                    OwnerIsConfigured = table.Column<bool>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerUserName = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerConfiguredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    ProgressPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationJobs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "ServerProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    InstallDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CacheDirectory = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Branch = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultPort = table.Column<int>(type: "INTEGER", nullable: false),
                    UdpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    RconPort = table.Column<int>(type: "INTEGER", nullable: false),
                    UseSteam = table.Column<bool>(type: "INTEGER", nullable: false),
                    AdminUsername = table.Column<string>(type: "TEXT", nullable: true),
                    AdminPassword = table.Column<string>(type: "TEXT", nullable: true),
                    BindIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PreferredMemoryInGigabytes = table.Column<int>(type: "INTEGER", nullable: false),
                    StartWithHost = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoRestartOnCrash = table.Column<bool>(type: "INTEGER", nullable: false),
                    WorkshopItemIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EnabledModIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MapFoldersJson = table.Column<string>(type: "TEXT", nullable: false),
                    ScheduledBackupsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScheduledBackupRetentionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PreUpdateBackupRetentionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    KeepManualBackupsForever = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreUpdateBackupEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerProfiles", x => x.ProfileId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAtUtc",
                table: "AuditEntries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OperationJobs_CreatedAtUtc",
                table: "OperationJobs",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "HostSettings");

            migrationBuilder.DropTable(
                name: "OperationJobs");

            migrationBuilder.DropTable(
                name: "ServerProfiles");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
