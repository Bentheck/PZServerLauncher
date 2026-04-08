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
        });

        builder.Entity<HostSettingsEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RemoteBindAddress).HasMaxLength(64);
            entity.Property(x => x.PublicHostname).HasMaxLength(255);
            entity.Property(x => x.CertificatePath).HasMaxLength(500);
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
    }
}
