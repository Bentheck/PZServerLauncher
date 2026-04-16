using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Host.Data.Entities;

namespace PZServerLauncher.Host.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ServerProfileEntity> ServerProfiles => Set<ServerProfileEntity>();

    public DbSet<HostSettingsEntity> HostSettings => Set<HostSettingsEntity>();

    public DbSet<OperationJobEntity> OperationJobs => Set<OperationJobEntity>();

    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();

    public DbSet<SettingsDraftEntity> SettingsDrafts => Set<SettingsDraftEntity>();

    public DbSet<NamedWorkshopPresetEntity> NamedWorkshopPresets => Set<NamedWorkshopPresetEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ServerProfileEntity>(entity =>
        {
            entity.HasKey(x => x.ProfileId);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.ServerName).HasMaxLength(200);
            entity.Property(x => x.InstallDirectory).HasMaxLength(500);
            entity.Property(x => x.CacheDirectory).HasMaxLength(500);
            entity.Property(x => x.BindIp).HasMaxLength(64);
            entity.Property(x => x.ScheduledBackupStartLocalTime).HasMaxLength(5);
        });

        builder.Entity<HostSettingsEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RemoteBindAddress).HasMaxLength(64);
            entity.Property(x => x.PublicHostname).HasMaxLength(255);
            entity.Property(x => x.CertificatePath).HasMaxLength(500);
            entity.Property(x => x.ProtectedSteamWebApiKey).HasColumnType("TEXT");
        });

        builder.Entity<OperationJobEntity>(entity =>
        {
            entity.HasKey(x => x.JobId);
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        builder.Entity<AuditEntryEntity>(entity =>
        {
            entity.HasKey(x => x.EntryId);
            entity.HasIndex(x => x.OccurredAtUtc);
        });

        builder.Entity<SettingsDraftEntity>(entity =>
        {
            entity.HasKey(x => new
            {
                x.ProfileId,
                x.Branch,
                x.CatalogId,
                x.CatalogVersion,
                x.PageId,
            });
            entity.Property(x => x.ProfileId).HasMaxLength(64);
            entity.Property(x => x.CatalogId).HasMaxLength(128);
            entity.Property(x => x.PageId).HasMaxLength(64);
            entity.Property(x => x.ValuesJson).HasColumnType("TEXT");
            entity.HasIndex(x => x.UpdatedAtUtc);
        });

        builder.Entity<NamedWorkshopPresetEntity>(entity =>
        {
            entity.HasKey(x => x.PresetId);
            entity.Property(x => x.ProfileId).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.NormalizedName).HasMaxLength(120);
            entity.Property(x => x.WorkshopItemIdsJson).HasColumnType("TEXT");
            entity.Property(x => x.EnabledModIdsJson).HasColumnType("TEXT");
            entity.Property(x => x.MapFoldersJson).HasColumnType("TEXT");
            entity.HasIndex(x => new { x.ProfileId, x.NormalizedName }).IsUnique();
            entity.HasIndex(x => x.UpdatedAtUtc);
        });
    }
}
