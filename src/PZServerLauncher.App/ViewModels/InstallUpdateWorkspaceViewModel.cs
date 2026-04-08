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

    public string BranchChannelSummary => SelectedProfile?.BranchChannelSummary ?? "No branch channel summary available.";

    public string SteamCmdCommandSummary => SelectedProfile?.SteamCmdCommandSummary ?? "SteamCMD command summary unavailable.";

    public string InstallReadiness => SelectedProfile?.InstallFootprintSummary ?? "No install profile selected.";

    public string CacheReadiness => SelectedProfile?.CacheFootprintSummary ?? "No cache profile selected.";

    public string BackupSafety => SelectedProfile?.BackupSafetySummary ?? "No backup context available.";

    public string NextRecommendedAction => SelectedProfile is null
        ? "Pick a profile to continue."
        : !SelectedProfile.IsInstallDetected
            ? "Run Install first for this branch."
            : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "Use Update only when you are ready for a maintenance window, or stop/restart from this page."
                : "The install looks ready. Start the server, or update it if you want to refresh the binaries before launch.";

    public string LifecycleGuidance => SelectedProfile?.RuntimePolicySummary ?? "No runtime state available.";

    public string LaunchReadiness => SelectedProfile?.LaunchReadinessSummary ?? "Launch readiness is unavailable.";

    public string PreflightSummary => SelectedProfile?.InstallPreflightSummary ?? "No preflight summary available.";

    public string ExpectedLauncherPath => SelectedProfile?.ExpectedLauncherPath ?? "No launcher path available.";

    public string ConfigFootprintSummary => SelectedProfile is null
        ? "No config footprint available."
        : $"{(SelectedProfile.ConfigDirectoryDetected ? "Config root detected" : "Config root missing")} | {(SelectedProfile.IniDetected ? "INI present" : "INI missing")} | {(SelectedProfile.SandboxDetected ? "Sandbox present" : "Sandbox missing")} | {(SelectedProfile.WorldDetected ? "World save detected" : "World save not created yet")}";

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
        OnPropertyChanged(nameof(BranchChannelSummary));
        OnPropertyChanged(nameof(SteamCmdCommandSummary));
        OnPropertyChanged(nameof(InstallReadiness));
        OnPropertyChanged(nameof(CacheReadiness));
        OnPropertyChanged(nameof(BackupSafety));
        OnPropertyChanged(nameof(NextRecommendedAction));
        OnPropertyChanged(nameof(LifecycleGuidance));
        OnPropertyChanged(nameof(LaunchReadiness));
        OnPropertyChanged(nameof(PreflightSummary));
        OnPropertyChanged(nameof(ExpectedLauncherPath));
        OnPropertyChanged(nameof(ConfigFootprintSummary));
    }
}
