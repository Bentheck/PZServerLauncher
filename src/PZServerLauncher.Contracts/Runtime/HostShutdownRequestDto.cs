namespace PZServerLauncher.Contracts.Runtime;

public sealed record HostShutdownRequestDto(
    bool StopRunningServers);
