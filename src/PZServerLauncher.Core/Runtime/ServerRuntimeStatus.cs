namespace PZServerLauncher.Core.Runtime;

public sealed record ServerRuntimeStatus(
    string ProfileId,
    ServerRuntimeState State,
    int? ProcessId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    string? LastExitReason,
    string? LatestLogLine);
