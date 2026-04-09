namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProfilePathUpdateDto(
    string InstallDirectory,
    string CacheDirectory);
