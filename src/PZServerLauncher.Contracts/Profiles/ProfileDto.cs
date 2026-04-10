using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProfileDto(
    string ProfileId,
    string DisplayName,
    string ServerName,
    string InstallDirectory,
    string CacheDirectory,
    ProjectZomboidBranch Branch,
    int DefaultPort,
    int UdpPort,
    int RconPort,
    string? BindIp,
    string? AdminUsername,
    int PreferredMemoryInGigabytes,
    bool StartWithHost,
    bool AutoRestartOnCrash,
    WorkshopPreset WorkshopPreset,
    BackupPolicy BackupPolicy);
