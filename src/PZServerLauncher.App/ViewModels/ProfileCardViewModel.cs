using CommunityToolkit.Mvvm.ComponentModel;

namespace PZServerLauncher.App.ViewModels;

public partial class ProfileCardViewModel : ViewModelBase
{
    public ProfileCardViewModel(
        string profileId,
        string displayName,
        string branch,
        string ports,
        string runtimeState,
        string installDirectory,
        string cacheDirectory,
        string lastBackup,
        string latestLogLine,
        bool hasBackup)
    {
        ProfileId = profileId;
        DisplayName = displayName;
        Branch = branch;
        Ports = ports;
        RuntimeState = runtimeState;
        InstallDirectory = installDirectory;
        CacheDirectory = cacheDirectory;
        LastBackup = lastBackup;
        LatestLogLine = latestLogLine;
        HasBackup = hasBackup;
    }

    public string ProfileId { get; }

    public string DisplayName { get; }

    public string Branch { get; }

    public string Ports { get; }

    public string InstallDirectory { get; }

    public string CacheDirectory { get; }

    [ObservableProperty]
    private string runtimeState;

    [ObservableProperty]
    private string lastBackup;

    [ObservableProperty]
    private string latestLogLine;

    [ObservableProperty]
    private bool hasBackup;
}
