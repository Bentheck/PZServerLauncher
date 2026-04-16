namespace PZServerLauncher.Core.Profiles;

public sealed record BackupPolicy
{
    public const int DefaultScheduledBackupRetentionCount = 10;
    public const int DefaultPreUpdateBackupRetentionCount = 5;
    public const int DefaultScheduledBackupIntervalHours = 6;
    public const string DefaultScheduledBackupStartLocalTime = "03:00";

    public static BackupPolicy Default { get; } = new();

    public bool ScheduledBackupsEnabled { get; init; }

    public int ScheduledBackupRetentionCount { get; init; } = DefaultScheduledBackupRetentionCount;

    public int ScheduledBackupIntervalHours { get; init; } = DefaultScheduledBackupIntervalHours;

    public string ScheduledBackupStartLocalTime { get; init; } = DefaultScheduledBackupStartLocalTime;

    public int PreUpdateBackupRetentionCount { get; init; } = DefaultPreUpdateBackupRetentionCount;

    public bool KeepManualBackupsForever { get; init; } = true;

    public bool PreUpdateBackupEnabled { get; init; } = true;
}
