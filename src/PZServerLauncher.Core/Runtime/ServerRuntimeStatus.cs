namespace PZServerLauncher.Core.Runtime;

public sealed record ServerRuntimeStatus(
    string ProfileId,
    ServerRuntimeState State,
    int? ProcessId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    string? LastExitReason,
    string? LatestLogLine,
    int ConnectedPlayerCount = 0,
    DateTimeOffset? LastPlayerActivityAtUtc = null,
    string? LastOperatorCommandSummary = null,
    WorkshopDownloadProgress? WorkshopDownloadProgress = null)
{
    public string? PinnedLatestSignal => WorkshopDownloadProgress?.DetailLabel ?? LatestLogLine;
}
