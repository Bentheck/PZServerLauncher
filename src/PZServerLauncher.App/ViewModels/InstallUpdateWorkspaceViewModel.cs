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
        : $"Install and lifecycle controls for {SelectedProfile.DisplayName}.";

    public string Branch => SelectedProfile?.Branch ?? "No profile selected";

    public string RuntimeState => SelectedProfile?.RuntimeState ?? "Unknown";

    public string InstallDirectory => SelectedProfile?.InstallDirectory ?? "No install path available";

    public string CacheDirectory => SelectedProfile?.CacheDirectory ?? "No cache path available";

    public string LastJobSummary => Legacy.RecentJobs.Count == 0
        ? "No recent jobs recorded."
        : Legacy.RecentJobs[0].Title + " | " + Legacy.RecentJobs[0].Detail;

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
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(RuntimeState));
        OnPropertyChanged(nameof(InstallDirectory));
        OnPropertyChanged(nameof(CacheDirectory));
        OnPropertyChanged(nameof(LastJobSummary));
    }
}
