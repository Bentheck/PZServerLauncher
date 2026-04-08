namespace PZServerLauncher.Core.Profiles;

public sealed record ServerProfile
{
    public required string ProfileId { get; init; }

    public required string DisplayName { get; init; }

    public required string ServerName { get; init; }

    public required string InstallDirectory { get; init; }

    public required string CacheDirectory { get; init; }

    public ProjectZomboidBranch Branch { get; init; } = ProjectZomboidBranch.Unstable42;

    public int DefaultPort { get; init; } = 16261;

    public int UdpPort { get; init; } = 16261;

    public int RconPort { get; init; } = 27015;

    public bool UseSteam { get; init; } = true;

    public string? AdminUsername { get; init; }

    public string? AdminPassword { get; init; }

    public string? BindIp { get; init; }

    public int PreferredMemoryInGigabytes { get; init; } = 6;

    public bool StartWithHost { get; init; }

    public bool AutoRestartOnCrash { get; init; } = true;

    public WorkshopPreset WorkshopPreset { get; init; } = WorkshopPreset.Empty;

    public BackupPolicy BackupPolicy { get; init; } = BackupPolicy.Default;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
