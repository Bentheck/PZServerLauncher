using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private readonly RuntimeEventStream _runtimeEventStream;

    public MainWindowViewModel()
        : this(new LocalHostApiClient(), new RuntimeEventStream(new LocalHostApiClient()))
    {
    }

    public MainWindowViewModel(LocalHostApiClient hostApiClient, RuntimeEventStream runtimeEventStream)
    {
        _hostApiClient = hostApiClient;
        _runtimeEventStream = runtimeEventStream;
        Title = "Project Zomboid Server Launcher";
        Subtitle = "Desktop control for the local PZServerLauncher host.";
        HostSummary = "Waiting for local host...";
        RemoteSummary = "Remote access status unavailable.";
        OwnerSummary = "Owner bootstrap status unavailable.";
        StatusMessage = "Starting up...";

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateStarterProfileCommand = new AsyncRelayCommand(CreateStarterProfileAsync);
        DiscoverImportsCommand = new AsyncRelayCommand(DiscoverImportsAsync);
        BootstrapOwnerCommand = new AsyncRelayCommand(BootstrapOwnerAsync);
        ImportCandidateCommand = new AsyncRelayCommand<ImportCandidateViewModel>(ImportCandidateAsync);
        InstallCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.InstallAsync, "Install"));
        UpdateCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.UpdateAsync, "Update"));
        StartCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.StartAsync, "Start"));
        StopCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.StopAsync, "Stop"));
        RestartCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.RestartAsync, "Restart"));
        BackupCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _hostApiClient.BackupAsync, "Backup"));
        RestoreCommand = new AsyncRelayCommand<ProfileCardViewModel>(RestoreLatestBackupAsync);
        SaveCommonConfigCommand = new AsyncRelayCommand<ProfileCardViewModel>(SaveCommonConfigAsync);
        ScanWorkshopCommand = new AsyncRelayCommand<ProfileCardViewModel>(ScanWorkshopAsync);

        _runtimeEventStream.StatusChanged += OnStatusChangedAsync;
        _runtimeEventStream.JobChanged += OnJobChangedAsync;
        _runtimeEventStream.LogLineReceived += OnLogLineReceivedAsync;

        _ = InitializeAsync();
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

    public ObservableCollection<ImportCandidateViewModel> ImportCandidates { get; } = [];

    public ObservableCollection<ShellItemViewModel> RecentJobs { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand CreateStarterProfileCommand { get; }

    public IAsyncRelayCommand DiscoverImportsCommand { get; }

    public IAsyncRelayCommand BootstrapOwnerCommand { get; }

    public IAsyncRelayCommand<ImportCandidateViewModel> ImportCandidateCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> InstallCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> UpdateCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> StartCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> StopCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> RestartCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> BackupCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> RestoreCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> SaveCommonConfigCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> ScanWorkshopCommand { get; }

    private async Task InitializeAsync()
    {
        await RefreshAsync();

        try
        {
            await _runtimeEventStream.EnsureConnectedAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Live runtime stream unavailable: {ex.Message}";
        }
    }

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
                    backups is { Count: > 0 },
                    profile.ServerName,
                    profile.DefaultPort.ToString(),
                    profile.UdpPort.ToString(),
                    profile.RconPort.ToString(),
                    profile.BindIp ?? string.Empty,
                    profile.AdminUsername ?? string.Empty,
                    profile.PreferredMemoryInGigabytes.ToString(),
                    profile.StartWithHost,
                    profile.AutoRestartOnCrash,
                    FormatWorkshopSummary(profile.WorkshopPreset),
                    "Workshop validation has not been run yet."));
            }

            RecentJobs.Clear();
            foreach (var job in snapshot.Jobs)
            {
                RecentJobs.Add(new ShellItemViewModel(
                    job.JobId.ToString("N"),
                    $"{job.Kind} - {job.Status}",
                    job.Detail ?? job.Summary));
            }

            StatusMessage = Profiles.Count == 0
                ? "Host is online. Create or import a profile to begin."
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

    private async Task DiscoverImportsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var candidates = await _hostApiClient.DiscoverLocalImportsAsync(CancellationToken.None) ?? [];
            ImportCandidates.Clear();

            foreach (var candidate in candidates)
            {
                ImportCandidates.Add(new ImportCandidateViewModel(
                    candidate.CandidateId,
                    candidate.DisplayName,
                    candidate.ServerName,
                    candidate.CacheDirectory,
                    candidate.InstallDirectory ?? "No install detected",
                    candidate.Branch.ToString(),
                    candidate.Diagnostics.Count == 0 ? "Ready to import." : string.Join(" ", candidate.Diagnostics),
                    candidate.IsAlreadyImported));
            }

            StatusMessage = ImportCandidates.Count == 0
                ? "No existing local Zomboid server configs were found."
                : $"Found {ImportCandidates.Count} import candidate(s).";
        }, "Scanning for existing local servers...");
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

    private async Task ImportCandidateAsync(ImportCandidateViewModel? candidate)
    {
        if (candidate is null || candidate.IsAlreadyImported)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _hostApiClient.ImportLocalCandidateAsync(candidate.CandidateId, CancellationToken.None);
            await RefreshAsync();
            await DiscoverImportsAsync();
            StatusMessage = $"Imported {candidate.DisplayName}.";
        }, $"Importing {candidate.DisplayName}...");
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

    private async Task SaveCommonConfigAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        if (!TryBuildCommonConfig(profile, out var commonConfig, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Common config is invalid.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _hostApiClient.UpdateCommonConfigAsync(profile.ProfileId, commonConfig!, CancellationToken.None);
            await RefreshAsync();
            StatusMessage = $"Saved common settings for {profile.DisplayName}.";
        }, $"Saving settings for {profile.DisplayName}...");
    }

    private async Task ScanWorkshopAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.ScanWorkshopAsync(profile.ProfileId, CancellationToken.None);
            if (result is null)
            {
                StatusMessage = "Workshop scan did not return a result.";
                return;
            }

            profile.WorkshopSummary = FormatWorkshopSummary(result.Preset);
            profile.WorkshopDiagnostics = result.Diagnostics.Count == 0
                ? "Workshop validation passed."
                : string.Join(" ", result.Diagnostics);
            StatusMessage = $"Workshop scan completed for {profile.DisplayName}.";
        }, $"Scanning workshop content for {profile.DisplayName}...");
    }

    private Task OnStatusChangedAsync(ServerRuntimeStatus status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var profile = Profiles.FirstOrDefault(x => x.ProfileId == status.ProfileId);
            if (profile is null)
            {
                return;
            }

            profile.RuntimeState = status.State.ToString();
            if (!string.IsNullOrWhiteSpace(status.LatestLogLine))
            {
                profile.LatestLogLine = status.LatestLogLine;
            }
        });

        return Task.CompletedTask;
    }

    private Task OnJobChangedAsync(OperationJob job)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var key = job.JobId.ToString("N");
            var existing = RecentJobs.FirstOrDefault(x => x.Key == key);
            if (existing is null)
            {
                RecentJobs.Insert(0, new ShellItemViewModel(
                    key,
                    $"{job.Kind} - {job.Status}",
                    job.Detail ?? job.Summary));

                while (RecentJobs.Count > 10)
                {
                    RecentJobs.RemoveAt(RecentJobs.Count - 1);
                }
            }
            else
            {
                existing.Title = $"{job.Kind} - {job.Status}";
                existing.Detail = job.Detail ?? job.Summary;
            }

            StatusMessage = job.Detail ?? job.Summary;
        });

        return Task.CompletedTask;
    }

    private Task OnLogLineReceivedAsync(string profileId, string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var profile = Profiles.FirstOrDefault(x => x.ProfileId == profileId);
            if (profile is not null)
            {
                profile.LatestLogLine = line;
            }
        });

        return Task.CompletedTask;
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

    private static string FormatWorkshopSummary(WorkshopPreset preset) =>
        $"{preset.WorkshopItemIds.Count} workshop / {preset.EnabledModIds.Count} mods / {preset.MapFolders.Count} maps";

    private static bool TryBuildCommonConfig(ProfileCardViewModel profile, out CommonConfigDto? config, out string? errorMessage)
    {
        config = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(profile.EditableServerName))
        {
            errorMessage = "Server name is required.";
            return false;
        }

        if (!int.TryParse(profile.EditableDefaultPort, out var defaultPort) ||
            !int.TryParse(profile.EditableUdpPort, out var udpPort) ||
            !int.TryParse(profile.EditableRconPort, out var rconPort) ||
            !int.TryParse(profile.EditableMemoryInGigabytes, out var memoryInGigabytes))
        {
            errorMessage = "Ports and memory must be valid whole numbers.";
            return false;
        }

        config = new CommonConfigDto(
            profile.EditableServerName.Trim(),
            defaultPort,
            udpPort,
            rconPort,
            string.IsNullOrWhiteSpace(profile.EditableBindIp) ? null : profile.EditableBindIp.Trim(),
            string.IsNullOrWhiteSpace(profile.EditableAdminUsername) ? null : profile.EditableAdminUsername.Trim(),
            memoryInGigabytes,
            profile.EditableStartWithHost,
            profile.EditableAutoRestartOnCrash);
        return true;
    }
}
