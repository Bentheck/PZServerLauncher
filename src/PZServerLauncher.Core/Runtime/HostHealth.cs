namespace PZServerLauncher.Core.Runtime;

public sealed record HostHealth(
    bool IsHealthy,
    string Version,
    int LoopbackPort,
    bool RemoteAccessEnabled,
    string? RemoteBaseUrl,
    DateTimeOffset StartedAtUtc,
    int RunningProfileCount);
