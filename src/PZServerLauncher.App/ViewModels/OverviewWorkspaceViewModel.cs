using CommunityToolkit.Mvvm.Input;

namespace PZServerLauncher.App.ViewModels;

public sealed class OverviewWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    public OverviewWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "overview",
            "Overview",
            "Runtime state, backups, and quick actions for the selected profile.",
            "Overview has no unsaved draft.",
            legacy,
            ["Runtime state", "Latest log", "Backup summary", "Quick actions"])
    {
        InstallCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.InstallCommand));
        UpdateCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.UpdateCommand));
        StartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StartCommand));
        StopCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StopCommand));
        RestartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.RestartCommand));
        BackupCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.BackupCommand));
        RestoreCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.RestoreCommand));
    }

    public IAsyncRelayCommand InstallCommand { get; }

    public IAsyncRelayCommand UpdateCommand { get; }

    public IAsyncRelayCommand StartCommand { get; }

    public IAsyncRelayCommand StopCommand { get; }

    public IAsyncRelayCommand RestartCommand { get; }

    public IAsyncRelayCommand BackupCommand { get; }

    public IAsyncRelayCommand RestoreCommand { get; }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to view runtime state, backup health, and quick actions."
        : $"Live runtime summary for {SelectedProfile.DisplayName}, including install health, backup posture, and the latest server activity.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string RuntimeState => SelectedProfile?.RuntimeState ?? "No profile selected";

    public string Ports => SelectedProfile?.Ports ?? "No ports available";

    public string InstallDirectory => SelectedProfile?.InstallDirectory ?? "No install path available";

    public string CacheDirectory => SelectedProfile?.CacheDirectory ?? "No cache path available";

    public string LatestLogLine => SelectedProfile?.LatestLogLine ?? "No recent log line.";

    public string LatestBackup => SelectedProfile?.LastBackup ?? "No backups yet.";

    public bool CanRestore => SelectedProfile?.HasBackup == true;

    public string InstallHealth => SelectedProfile is null
        ? "Install path unavailable."
        : Directory.Exists(SelectedProfile.InstallDirectory)
            ? "Install directory detected and ready for host actions."
            : "Install directory has not been detected yet. Run Install before first launch.";

    public string CacheHealth => SelectedProfile is null
        ? "Cache path unavailable."
        : Directory.Exists(SelectedProfile.CacheDirectory)
            ? "Cache directory is present."
            : "Cache directory does not exist yet. Starting or importing the profile will create it.";

    public string BackupHealth => SelectedProfile is null
        ? "No backup information is available."
        : SelectedProfile.HasBackup
            ? $"Latest backup is {SelectedProfile.LastBackup}."
            : "No backup archive has been captured yet. Create one before major config or update work.";

    public string MemorySummary => SelectedProfile is null
        ? "No memory profile selected."
        : $"{SelectedProfile.EditableMemoryInGigabytes} GB preferred memory, {(SelectedProfile.EditableStartWithHost ? "starts with host" : "manual start")}, {(SelectedProfile.EditableAutoRestartOnCrash ? "auto-restart on crash enabled" : "auto-restart on crash disabled")}.";

    public string WorkshopSummary => SelectedProfile?.WorkshopSummary ?? "No workshop profile loaded.";

    public string OperatorGuidance => SelectedProfile is null
        ? "Pick or import a profile to start managing a server."
        : !Directory.Exists(SelectedProfile.InstallDirectory)
            ? "Install this branch first, then return here to launch or configure the server."
            : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "The server is currently live. Use Logs, Backups, and Mods & Maps for the most common active-admin tasks."
                : "The server is installed and idle. Review settings, capture a backup, then start it from this page or Install & Update.";

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
        OnPropertyChanged(nameof(Ports));
        OnPropertyChanged(nameof(InstallDirectory));
        OnPropertyChanged(nameof(CacheDirectory));
        OnPropertyChanged(nameof(LatestLogLine));
        OnPropertyChanged(nameof(LatestBackup));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(InstallHealth));
        OnPropertyChanged(nameof(CacheHealth));
        OnPropertyChanged(nameof(BackupHealth));
        OnPropertyChanged(nameof(MemorySummary));
        OnPropertyChanged(nameof(WorkshopSummary));
        OnPropertyChanged(nameof(OperatorGuidance));
    }
}
