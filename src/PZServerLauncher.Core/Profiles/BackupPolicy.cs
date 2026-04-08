namespace PZServerLauncher.Core.Profiles;

public sealed record BackupPolicy
{
    public static BackupPolicy Default { get; } = new();

    public bool ScheduledBackupsEnabled { get; init; }

    public int ScheduledBackupRetentionCount { get; init; } = 10;

    public int PreUpdateBackupRetentionCount { get; init; } = 5;

    public bool KeepManualBackupsForever { get; init; } = true;

    public bool PreUpdateBackupEnabled { get; init; } = true;
}
