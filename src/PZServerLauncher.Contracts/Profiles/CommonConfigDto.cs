namespace PZServerLauncher.Contracts.Profiles;

public sealed record CommonConfigDto(
    string ServerName,
    int DefaultPort,
    int UdpPort,
    int RconPort,
    string? BindIp,
    string? AdminUsername,
    int PreferredMemoryInGigabytes,
    bool StartWithHost,
    bool AutoRestartOnCrash);
