using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;

namespace PZServerLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;

    public MainWindowViewModel()
        : this(new LocalHostApiClient())
    {
    }

    public MainWindowViewModel(LocalHostApiClient hostApiClient)
    {
        _hostApiClient = hostApiClient;
        Title = "Project Zomboid Server Launcher";
        Subtitle = "Desktop control for the local PZServerLauncher host.";
        HostSummary = "Waiting for local host...";
        RemoteSummary = "Remote access status unavailable.";
        OwnerSummary = "Owner bootstrap status unavailable.";
        StatusMessage = "Starting up...";

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateStarterProfileCommand = new AsyncRelayCommand(CreateStarterProfileAsync);
        BootstrapOwnerCommand = new AsyncRelayCommand(BootstrapOwnerAsync);
        InstallCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.InstallAsync, "Install"));
        UpdateCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.UpdateAsync, "Update"));
        StartCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.StartAsync, "Start"));
        StopCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.StopAsync, "Stop"));
        RestartCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.RestartAsync, "Restart"));
        BackupCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.BackupAsync, "Backup"));
        RestoreCommand = new AsyncRelayCommand<ProfileCardViewModel>(RestoreLatestBackupAsync);

        _ = RefreshAsync();
    }

    public string Title { get; }

    public string Subtitle { get; }

    [ObservableProperty]
    private string hostSummary;

    [ObservableProperty]
    private string remoteSummary;

    [ObservableProperty]
    private string ownerSummary;

    [ObservableProperty]
    private bool ownerBootstrapRequired;

    [ObservableProperty]
    private string ownerUserName = "owner";

    [ObservableProperty]
    private string ownerEmail = "owner@localhost";

    [ObservableProperty]
    private string ownerPassword = string.Empty;

    [ObservableProperty]
    private string statusMessage;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<ProfileCardViewModel> Profiles { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand CreateStarterProfileCommand { get; }

    public IAsyncRelayCommand BootstrapOwnerCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> InstallCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> UpdateCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> StartCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> StopCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> RestartCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> BackupCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> RestoreCommand { get; }

    private async Task RefreshAsync()
    {
        await RunBusyAsync(async () =>
        {
            var snapshot = await _hostApiClient.LoadSnapshotAsync();
            HostSummary = $"Loopback {snapshot.HostInfo.Settings.LoopbackPort} | Running profiles {snapshot.HostInfo.Health.RunningProfileCount}";
            RemoteSummary = snapshot.HostInfo.Settings.RemoteAccess.IsEnabled
                ? $"Remote enabled on {snapshot.HostInfo.Settings.RemoteAccess.BindAddress}:{snapshot.HostInfo.Settings.RemoteAccess.HttpsPort}"
                : "Remote access disabled";
            OwnerBootstrapRequired = !snapshot.HostInfo.Settings.OwnerBootstrap.IsConfigured;
            OwnerSummary = snapshot.HostInfo.Settings.OwnerBootstrap.IsConfigured
                ? $"Owner account: {snapshot.HostInfo.Settings.OwnerBootstrap.OwnerUserName}"
                : "Owner bootstrap is still required before optional web admin can be enabled.";

            Profiles.Clear();
            foreach (var profile in snapshot.Profiles)
            {
                snapshot.Statuses.TryGetValue(profile.ProfileId, out var status);
                snapshot.Backups.TryGetValue(profile.ProfileId, out var backups);
                var latestBackup = backups?.FirstOrDefault() ?? "No backups";
                Profiles.Add(new ProfileCardViewModel(
                    profile.ProfileId,
                    profile.DisplayName,
                    profile.Branch.ToString(),
                    $"{profile.DefaultPort} / {profile.UdpPort} / {profile.RconPort}",
                    status?.State.ToString() ?? "Stopped",
                    profile.InstallDirectory,
                    profile.CacheDirectory,
                    latestBackup,
                    status?.LatestLogLine ?? "No recent log lines yet.",
                    backups is { Count: > 0 }));
            }

            StatusMessage = Profiles.Count == 0
                ? "Host is online. Create the starter profile to begin."
                : $"Loaded {Profiles.Count} profile(s).";
        }, "Refreshing host state...");
    }

    private async Task CreateStarterProfileAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _hostApiClient.CreateStarterProfileAsync();
            await RefreshAsync();
            StatusMessage = "Starter profile created.";
        }, "Creating starter profile...");
    }

    private async Task BootstrapOwnerAsync()
    {
        if (string.IsNullOrWhiteSpace(OwnerUserName) ||
            string.IsNullOrWhiteSpace(OwnerEmail) ||
            string.IsNullOrWhiteSpace(OwnerPassword))
        {
            StatusMessage = "Owner username, email, and password are all required.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.BootstrapOwnerAsync(OwnerUserName, OwnerEmail, OwnerPassword, CancellationToken.None);
            OwnerPassword = string.Empty;
            StatusMessage = result?.Message ?? "Owner account created.";
            await RefreshAsync();
        }, "Bootstrapping owner account...");
    }

    private async Task RunProfileActionAsync(
        ProfileCardViewModel? profile,
        Func<string, CancellationToken, Task<PZServerLauncher.Contracts.Runtime.OperationResultDto?>> action,
        string actionName)
    {
        if (profile is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await action(profile.ProfileId, CancellationToken.None);
            StatusMessage = result?.Message ?? $"{actionName} completed.";
            await RefreshAsync();
        }, $"{actionName} {profile.DisplayName}...");
    }

    private async Task RestoreLatestBackupAsync(ProfileCardViewModel? profile)
    {
        if (profile is null || !profile.HasBackup)
        {
            StatusMessage = "A backup is required before restore can run.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.RestoreAsync(profile.ProfileId, profile.LastBackup, restartAfterRestore: true, CancellationToken.None);
            StatusMessage = result?.Message ?? "Restore queued.";
            await RefreshAsync();
        }, $"Restoring {profile.DisplayName} from {profile.LastBackup}...");
    }

    private async Task RunBusyAsync(Func<Task> work, string busyMessage)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = busyMessage;
            await work();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
