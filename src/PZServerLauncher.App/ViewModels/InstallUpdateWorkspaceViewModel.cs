using CommunityToolkit.Mvvm.Input;

namespace PZServerLauncher.App.ViewModels;

public sealed class InstallUpdateWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    public InstallUpdateWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "install-update",
            "Install & Update",
            "Install state, branch, paths, and lifecycle actions for the selected profile.",
            "Install & Update has no unsaved draft.",
            legacy,
            ["Install path", "Update actions", "Runtime controls", "Recent jobs"])
    {
        InstallCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.InstallCommand));
        UpdateCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.UpdateCommand));
        StartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StartCommand));
        StopCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StopCommand));
        RestartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.RestartCommand));
        RefreshCommand = new AsyncRelayCommand(() => Legacy.RefreshCommand.ExecuteAsync(null));
    }

    public IAsyncRelayCommand InstallCommand { get; }

    public IAsyncRelayCommand UpdateCommand { get; }

    public IAsyncRelayCommand StartCommand { get; }

    public IAsyncRelayCommand StopCommand { get; }

    public IAsyncRelayCommand RestartCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to install, update, and control its runtime."
        : $"Install and lifecycle controls for {SelectedProfile.DisplayName}, with preflight context for branch, paths, and safety.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "No profile selected";

    public string RuntimeState => SelectedProfile?.RuntimeState ?? "Unknown";

    public string InstallDirectory => SelectedProfile?.InstallDirectory ?? "No install path available";

    public string CacheDirectory => SelectedProfile?.CacheDirectory ?? "No cache path available";

    public string LastJobSummary => Legacy.RecentJobs.Count == 0
        ? "No recent jobs recorded."
        : Legacy.RecentJobs[0].Title + " | " + Legacy.RecentJobs[0].Detail;

    public string InstallReadiness => SelectedProfile is null
        ? "No install profile selected."
        : Directory.Exists(SelectedProfile.InstallDirectory)
            ? "The install directory already exists. Update can refresh binaries in place."
            : "The install directory is missing. Install will create the local dedicated server footprint.";

    public string CacheReadiness => SelectedProfile is null
        ? "No cache profile selected."
        : Directory.Exists(SelectedProfile.CacheDirectory)
            ? "The cache directory is present and ready for server config, saves, and backups."
            : "The cache directory is not present yet. It will be created when the server profile is initialized.";

    public string BackupSafety => SelectedProfile is null
        ? "No backup context available."
        : SelectedProfile.HasBackup
            ? $"Latest backup: {SelectedProfile.LastBackup}. Updates will also trigger a pre-update safety backup."
            : "No backup archive exists yet. Updates still create a pre-update backup automatically, but taking a manual one now is safer.";

    public string NextRecommendedAction => SelectedProfile is null
        ? "Pick a profile to continue."
        : !Directory.Exists(SelectedProfile.InstallDirectory)
            ? "Run Install first for this branch."
            : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "Use Update only when you are ready for a maintenance window, or stop/restart from this page."
                : "The install looks ready. Start the server, or update it if you want to refresh the binaries before launch.";

    public string LifecycleGuidance => SelectedProfile is null
        ? "No runtime state available."
        : $"{SelectedProfile.RuntimeState} | Start with host: {(SelectedProfile.EditableStartWithHost ? "On" : "Off")} | Auto restart on crash: {(SelectedProfile.EditableAutoRestartOnCrash ? "On" : "Off")}";

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        Notify();
    }

    private async Task ExecuteProfileCommandAsync(IAsyncRelayCommand<ProfileCardViewModel> command)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await command.ExecuteAsync(SelectedProfile);
        Notify();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(RuntimeState));
        OnPropertyChanged(nameof(InstallDirectory));
        OnPropertyChanged(nameof(CacheDirectory));
        OnPropertyChanged(nameof(LastJobSummary));
        OnPropertyChanged(nameof(InstallReadiness));
        OnPropertyChanged(nameof(CacheReadiness));
        OnPropertyChanged(nameof(BackupSafety));
        OnPropertyChanged(nameof(NextRecommendedAction));
        OnPropertyChanged(nameof(LifecycleGuidance));
    }
}
