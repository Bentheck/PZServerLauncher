using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private readonly RuntimeEventStream _runtimeEventStream;
    private readonly DesktopShellService _desktopShellService;
    private HostSettings? _loadedHostSettings;

    public MainWindowViewModel()
        : this(new LocalHostApiClient(), new RuntimeEventStream(new LocalHostApiClient()), new DesktopShellService())
    {
    }

    public MainWindowViewModel(
        LocalHostApiClient hostApiClient,
        RuntimeEventStream runtimeEventStream,
        DesktopShellService desktopShellService)
    {
        _hostApiClient = hostApiClient;
        _runtimeEventStream = runtimeEventStream;
        _desktopShellService = desktopShellService;
        Title = "Project Zomboid Server Launcher";
        Subtitle = "Desktop control for the local PZServerLauncher host.";
        HostSummary = "Waiting for local host...";
        RemoteSummary = "Remote access status unavailable.";
        OwnerSummary = "Owner bootstrap status unavailable.";
        StatusMessage = "Starting up...";
        RemoteWizardStatus = "Remote access settings are ready for local validation.";
        RemoteSelfTestChecks = "Run the local self-test after saving your HTTPS settings.";
        RemoteBindAddress = "0.0.0.0";
        RemoteHttpsPort = ProjectZomboidDefaults.DefaultRemotePort.ToString();

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
        LoadRawConfigCommand = new AsyncRelayCommand<ProfileCardViewModel>(LoadRawConfigAsync);
        SaveRawConfigCommand = new AsyncRelayCommand<ProfileCardViewModel>(SaveRawConfigAsync);
        SaveHostSettingsCommand = new AsyncRelayCommand(SaveHostSettingsAsync);
        StopHostCommand = new AsyncRelayCommand(() => StopHostAsync(stopRunningServers: false));
        StopAllAndHostCommand = new AsyncRelayCommand(() => StopHostAsync(stopRunningServers: true));
        SaveRemoteAccessCommand = new AsyncRelayCommand(SaveRemoteAccessAsync);
        RemoteSelfTestCommand = new AsyncRelayCommand(RunRemoteSelfTestAsync);
        ApplyFirewallRuleCommand = new AsyncRelayCommand(ApplyFirewallRuleAsync);
        ExitDesktopCommand = new RelayCommand(() => _desktopShellService.ExitDesktop());

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

    [ObservableProperty]
    private bool hostStartWithWindows;

    [ObservableProperty]
    private bool remoteAccessEnabled;

    [ObservableProperty]
    private string remoteBindAddress;

    [ObservableProperty]
    private string remoteHttpsPort;

    [ObservableProperty]
    private string remotePublicHostname = string.Empty;

    [ObservableProperty]
    private string remoteCertificatePath = string.Empty;

    [ObservableProperty]
    private string remoteCertificatePassword = string.Empty;

    [ObservableProperty]
    private bool remoteCreateFirewallRule;

    [ObservableProperty]
    private string remoteWizardStatus;

    [ObservableProperty]
    private string remoteSelfTestChecks;

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

    public IAsyncRelayCommand<ProfileCardViewModel> LoadRawConfigCommand { get; }

    public IAsyncRelayCommand<ProfileCardViewModel> SaveRawConfigCommand { get; }

    public IAsyncRelayCommand SaveHostSettingsCommand { get; }

    public IAsyncRelayCommand StopHostCommand { get; }

    public IAsyncRelayCommand StopAllAndHostCommand { get; }

    public IAsyncRelayCommand SaveRemoteAccessCommand { get; }

    public IAsyncRelayCommand RemoteSelfTestCommand { get; }

    public IAsyncRelayCommand ApplyFirewallRuleCommand { get; }

    public IRelayCommand ExitDesktopCommand { get; }

    private async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await RunBusyAsync(async () =>
        {
            var snapshot = await _hostApiClient.LoadSnapshotAsync();
            _loadedHostSettings = snapshot.HostInfo.Settings;
            HostSummary = $"Loopback {snapshot.HostInfo.Settings.LoopbackPort} | Running profiles {snapshot.HostInfo.Health.RunningProfileCount}";
            RemoteSummary = snapshot.HostInfo.Settings.RemoteAccess.IsEnabled
                ? $"Remote enabled on {snapshot.HostInfo.Settings.RemoteAccess.BindAddress}:{snapshot.HostInfo.Settings.RemoteAccess.HttpsPort}"
                : "Remote access disabled";
            OwnerBootstrapRequired = !snapshot.HostInfo.Settings.OwnerBootstrap.IsConfigured;
            OwnerSummary = snapshot.HostInfo.Settings.OwnerBootstrap.IsConfigured
                ? $"Owner account: {snapshot.HostInfo.Settings.OwnerBootstrap.OwnerUserName}"
                : "Owner bootstrap is still required before optional web admin can be enabled.";
            HostStartWithWindows = snapshot.HostInfo.Settings.StartHostWithWindows;
            PopulateRemoteAccessSettings(snapshot.HostInfo.Settings.RemoteAccess);

            var postureTasks = snapshot.Profiles
                .Select(profile => LoadProfilePostureSummaryAsync(profile.ProfileId, profile.DisplayName))
                .ToArray();
            var postureResults = await Task.WhenAll(postureTasks);
            var postureLookup = postureResults.ToDictionary(result => result.ProfileId, result => result.Summary, StringComparer.OrdinalIgnoreCase);

            Profiles.Clear();
            foreach (var profile in snapshot.Profiles)
            {
                snapshot.Statuses.TryGetValue(profile.ProfileId, out var status);
                snapshot.Backups.TryGetValue(profile.ProfileId, out var backups);
                var latestBackup = backups?.FirstOrDefault() ?? "No backups";
                var posture = postureLookup.GetValueOrDefault(profile.ProfileId) ?? ProjectZomboidProfilePostureSummaryBuilder.Unavailable(profile.DisplayName);
                var installPosture = ProjectZomboidInstallPostureSummaryBuilder.Build(
                    ToServerProfile(profile),
                    status?.State.ToString() ?? "Stopped",
                    backups is { Count: > 0 },
                    latestBackup);

                Profiles.Add(new ProfileCardViewModel(
                    profile.ProfileId,
                    profile.DisplayName,
                    profile.Branch.ToString(),
                    profile.Branch,
                    $"{profile.DefaultPort} / {profile.UdpPort} / {profile.RconPort}",
                    status?.State.ToString() ?? "Stopped",
                    profile.InstallDirectory,
                    profile.CacheDirectory,
                    latestBackup,
                    status?.LatestLogLine ?? "No recent log lines yet.",
                    backups is { Count: > 0 },
                    profile.BackupPolicy,
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
                    "Workshop validation has not been run yet.",
                    posture,
                    installPosture));
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

            try
            {
                await _runtimeEventStream.EnsureConnectedAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Live runtime stream unavailable: {ex.Message}";
            }
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

    private async Task LoadRawConfigAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.GetRawConfigAsync(profile.ProfileId, profile.SelectedRawConfigKind.Kind, CancellationToken.None);
            if (result is null)
            {
                StatusMessage = $"Unable to load {profile.SelectedRawConfigKind.Label}.";
                return;
            }

            RawConfigEditorState.Apply(profile, result);
            StatusMessage = $"Loaded {profile.SelectedRawConfigKind.Label} for {profile.DisplayName}.";
        }, $"Loading {profile.SelectedRawConfigKind.Label} for {profile.DisplayName}...");
    }

    private async Task SaveRawConfigAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        if (!profile.IsRawConfigLoaded || profile.LoadedRawConfigKind != profile.SelectedRawConfigKind.Kind)
        {
            StatusMessage = $"Load {profile.SelectedRawConfigKind.Label} before saving it.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var payload = new RawConfigFileDto(
                profile.SelectedRawConfigKind.Kind,
                profile.RawConfigContent,
                profile.LoadedRawConfigSha256,
                []);

            var result = await _hostApiClient.SaveRawConfigAsync(profile.ProfileId, profile.SelectedRawConfigKind.Kind, payload, CancellationToken.None);
            if (result is null)
            {
                StatusMessage = $"Unable to save {profile.SelectedRawConfigKind.Label}.";
                return;
            }

            RawConfigEditorState.Apply(profile, result);
            StatusMessage = $"Saved {profile.SelectedRawConfigKind.Label} for {profile.DisplayName}.";
        }, $"Saving {profile.SelectedRawConfigKind.Label} for {profile.DisplayName}...");
    }

    private async Task SaveHostSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var current = _loadedHostSettings ?? await _hostApiClient.GetHostSettingsAsync(CancellationToken.None)
                ?? throw new InvalidOperationException("Host settings could not be loaded.");

            var updated = await _hostApiClient.UpdateHostSettingsAsync(current with
            {
                StartHostWithWindows = HostStartWithWindows,
            }, CancellationToken.None);

            _loadedHostSettings = updated ?? current with { StartHostWithWindows = HostStartWithWindows };
            HostStartWithWindows = _loadedHostSettings.StartHostWithWindows;
            StatusMessage = HostStartWithWindows
                ? "The local host will start with Windows for this user."
                : "The local host will no longer start with Windows.";
        }, "Saving host settings...");
    }

    private async Task StopHostAsync(bool stopRunningServers)
    {
        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.StopHostAsync(stopRunningServers, CancellationToken.None);
            await _runtimeEventStream.DisconnectAsync();

            HostSummary = "Local host stopped. Use Refresh to start it again.";
            RemoteSummary = "Remote access is unavailable while the local host is stopped.";
            RemoteWizardStatus = "Host stopped. Start the host again before running a live HTTPS self-test.";
            StatusMessage = result?.Message ?? "Local host shutdown requested.";
        }, stopRunningServers ? "Stopping all servers and the local host..." : "Stopping the local host...");
    }

    private async Task SaveRemoteAccessAsync()
    {
        if (!TryBuildRemoteAccessSettings(out var settings, out var errorMessage))
        {
            RemoteWizardStatus = errorMessage ?? "Remote access settings are invalid.";
            StatusMessage = RemoteWizardStatus;
            return;
        }

        await RunBusyAsync(async () =>
        {
            var saved = await _hostApiClient.UpdateRemoteAccessSettingsAsync(settings!, CancellationToken.None)
                ?? throw new InvalidOperationException("Remote access settings could not be saved.");

            PopulateRemoteAccessSettings(new RemoteAccessSettings
            {
                IsEnabled = saved.IsEnabled,
                BindAddress = saved.BindAddress,
                HttpsPort = saved.HttpsPort,
                PublicHostname = saved.PublicHostname,
                CertificatePath = saved.CertificatePath,
                CreateFirewallRule = saved.CreateFirewallRule,
                RequiresHostRestart = true,
            });
            RemoteCertificatePassword = string.Empty;
            RemoteWizardStatus = saved.IsEnabled
                ? "Remote access settings saved. Restart the host to apply HTTPS binding changes."
                : "Remote access settings saved. The HTTPS listener remains disabled.";

            if (saved.IsEnabled && saved.CreateFirewallRule)
            {
                var firewall = await _hostApiClient.ApplyRemoteFirewallRuleAsync(settings!, CancellationToken.None);
                RemoteWizardStatus = $"{RemoteWizardStatus} {firewall?.Message}";
            }

            StatusMessage = RemoteWizardStatus;
            await RefreshAsync();
        }, "Saving remote access settings...");
    }

    private async Task RunRemoteSelfTestAsync()
    {
        if (!TryBuildRemoteAccessSettings(out var settings, out var errorMessage))
        {
            RemoteWizardStatus = errorMessage ?? "Remote access settings are invalid.";
            StatusMessage = RemoteWizardStatus;
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.RunRemoteAccessSelfTestAsync(settings!, CancellationToken.None)
                ?? throw new InvalidOperationException("Remote self-test did not return a result.");

            RemoteWizardStatus = result.Summary;
            RemoteSelfTestChecks = string.Join(Environment.NewLine, result.Checks);
            StatusMessage = result.Summary;
        }, "Running local HTTPS self-test...");
    }

    private async Task ApplyFirewallRuleAsync()
    {
        if (!TryBuildRemoteAccessSettings(out var settings, out var errorMessage))
        {
            RemoteWizardStatus = errorMessage ?? "Remote access settings are invalid.";
            StatusMessage = RemoteWizardStatus;
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.ApplyRemoteFirewallRuleAsync(settings!, CancellationToken.None)
                ?? throw new InvalidOperationException("Firewall rule update did not return a result.");
            RemoteWizardStatus = result.Message;
            StatusMessage = result.Message;
        }, "Updating the Windows Firewall rule...");
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

    private static ServerProfile ToServerProfile(ProfileDto profile) =>
        new()
        {
            ProfileId = profile.ProfileId,
            DisplayName = profile.DisplayName,
            ServerName = profile.ServerName,
            InstallDirectory = profile.InstallDirectory,
            CacheDirectory = profile.CacheDirectory,
            Branch = profile.Branch,
            DefaultPort = profile.DefaultPort,
            UdpPort = profile.UdpPort,
            RconPort = profile.RconPort,
            UseSteam = true,
            AdminUsername = profile.AdminUsername,
            BindIp = profile.BindIp,
            PreferredMemoryInGigabytes = profile.PreferredMemoryInGigabytes,
            StartWithHost = profile.StartWithHost,
            AutoRestartOnCrash = profile.AutoRestartOnCrash,
            WorkshopPreset = profile.WorkshopPreset,
            BackupPolicy = profile.BackupPolicy,
        };

    private async Task<(string ProfileId, ProjectZomboidProfilePostureSummary Summary)> LoadProfilePostureSummaryAsync(string profileId, string displayName)
    {
        try
        {
            var generalTask = _hostApiClient.GetSettingsPageAsync(profileId, ProfileWorkspacePageIds.General, CancellationToken.None);
            var networkTask = _hostApiClient.GetSettingsPageAsync(profileId, ProfileWorkspacePageIds.NetworkAndAdmin, CancellationToken.None);
            var sandboxTask = _hostApiClient.GetSettingsPageAsync(profileId, ProfileWorkspacePageIds.Sandbox, CancellationToken.None);
            await Task.WhenAll(generalTask, networkTask, sandboxTask);

            var generalValues = generalTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            var networkValues = networkTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            var sandboxValues = sandboxTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);

            return (
                profileId,
                ProjectZomboidProfilePostureSummaryBuilder.Build(
                    displayName,
                    generalValues,
                    networkValues,
                    sandboxValues));
        }
        catch
        {
            return (profileId, ProjectZomboidProfilePostureSummaryBuilder.Unavailable(displayName));
        }
    }

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

    private bool TryBuildRemoteAccessSettings(out RemoteAccessSettingsDto? settings, out string? errorMessage)
    {
        settings = null;
        errorMessage = null;

        if (!int.TryParse(RemoteHttpsPort, out var httpsPort))
        {
            errorMessage = "Remote HTTPS port must be a whole number.";
            return false;
        }

        settings = new RemoteAccessSettingsDto(
            RemoteAccessEnabled,
            string.IsNullOrWhiteSpace(RemoteBindAddress) ? "0.0.0.0" : RemoteBindAddress.Trim(),
            httpsPort,
            string.IsNullOrWhiteSpace(RemotePublicHostname) ? null : RemotePublicHostname.Trim(),
            string.IsNullOrWhiteSpace(RemoteCertificatePath) ? null : RemoteCertificatePath.Trim(),
            string.IsNullOrWhiteSpace(RemoteCertificatePassword) ? null : RemoteCertificatePassword,
            RemoteCreateFirewallRule);
        return true;
    }

    private void PopulateRemoteAccessSettings(RemoteAccessSettings settings)
    {
        RemoteAccessEnabled = settings.IsEnabled;
        RemoteBindAddress = settings.BindAddress;
        RemoteHttpsPort = settings.HttpsPort.ToString();
        RemotePublicHostname = settings.PublicHostname ?? string.Empty;
        RemoteCertificatePath = settings.CertificatePath ?? string.Empty;
        RemoteCertificatePassword = string.Empty;
        RemoteCreateFirewallRule = settings.CreateFirewallRule;
    }
}
