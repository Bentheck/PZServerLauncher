namespace PZServerLauncher.Contracts.Runtime;

public sealed record LauncherUpdateStatusDto(
    LauncherUpdateState State,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseTitle,
    string? ReleasePageUrl,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset CheckedAtUtc,
    string StatusMessage);
