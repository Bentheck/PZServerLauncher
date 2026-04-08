namespace PZServerLauncher.Core.Profiles;

public sealed record ServerProfile(
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
    int PreferredMemoryInGigabytes);
