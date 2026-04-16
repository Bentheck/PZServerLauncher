using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Host.Data.Entities;

public sealed class ServerProfileEntity
{
    public required string ProfileId { get; set; }

    public required string DisplayName { get; set; }

    public required string ServerName { get; set; }

    public required string InstallDirectory { get; set; }

    public required string CacheDirectory { get; set; }

    public int Branch { get; set; }

    public int DefaultPort { get; set; }

    public int UdpPort { get; set; }

    public int RconPort { get; set; }

    public bool UseSteam { get; set; }

    public string? AdminUsername { get; set; }

    public string? AdminPassword { get; set; }

    public string? BindIp { get; set; }

    public int PreferredMemoryInGigabytes { get; set; }

    public bool StartWithHost { get; set; }

    public bool AutoRestartOnCrash { get; set; }

    public string WorkshopItemIdsJson { get; set; } = "[]";

    public string EnabledModIdsJson { get; set; } = "[]";

    public string MapFoldersJson { get; set; } = "[]";

    public bool ScheduledBackupsEnabled { get; set; }

    public int ScheduledBackupRetentionCount { get; set; }

    public int ScheduledBackupIntervalHours { get; set; } = BackupPolicy.DefaultScheduledBackupIntervalHours;

    public string ScheduledBackupStartLocalTime { get; set; } = BackupPolicy.DefaultScheduledBackupStartLocalTime;

    public int PreUpdateBackupRetentionCount { get; set; }

    public bool KeepManualBackupsForever { get; set; }

    public bool PreUpdateBackupEnabled { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
