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
        : $"Live runtime summary for {SelectedProfile.DisplayName}.";

    public string RuntimeState => SelectedProfile?.RuntimeState ?? "No profile selected";

    public string Ports => SelectedProfile?.Ports ?? "No ports available";

    public string InstallDirectory => SelectedProfile?.InstallDirectory ?? "No install path available";

    public string CacheDirectory => SelectedProfile?.CacheDirectory ?? "No cache path available";

    public string LatestLogLine => SelectedProfile?.LatestLogLine ?? "No recent log line.";

    public string LatestBackup => SelectedProfile?.LastBackup ?? "No backups yet.";

    public bool CanRestore => SelectedProfile?.HasBackup == true;

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
        OnPropertyChanged(nameof(RuntimeState));
        OnPropertyChanged(nameof(Ports));
        OnPropertyChanged(nameof(InstallDirectory));
        OnPropertyChanged(nameof(CacheDirectory));
        OnPropertyChanged(nameof(LatestLogLine));
        OnPropertyChanged(nameof(LatestBackup));
        OnPropertyChanged(nameof(CanRestore));
    }
}
