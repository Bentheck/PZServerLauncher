namespace PZServerLauncher.App.ViewModels;

public sealed record ProfileCardViewModel(
    string ProfileId,
    string DisplayName,
    string Branch,
    string Ports,
    string RuntimeState,
    string InstallDirectory,
    string CacheDirectory,
    string LastBackup,
    string LatestLogLine,
    bool HasBackup);
