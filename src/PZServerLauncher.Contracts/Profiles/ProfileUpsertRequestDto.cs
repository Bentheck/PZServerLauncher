using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProfileUpsertRequestDto(
    string ProfileId,
    string DisplayName,
    string ServerName,
    string InstallDirectory,
    string CacheDirectory,
    ProjectZomboidBranch Branch,
    int DefaultPort,
    int UdpPort,
    int RconPort,
    bool UseSteam,
    string? AdminUsername,
    string? AdminPassword,
    string? BindIp,
    int PreferredMemoryInGigabytes,
    bool StartWithHost,
    bool AutoRestartOnCrash,
    WorkshopPreset WorkshopPreset,
    BackupPolicy BackupPolicy);
