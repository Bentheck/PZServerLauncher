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
using PZServerLauncher.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILauncherRuntime _runtime;
    private readonly DesktopShellService _desktopShellService;
    private readonly CreateProfileDialogService _createProfileDialogService;
    private HostSettings? _loadedHostSettings;
    private bool _attemptedInitialImportDiscovery;

    public MainWindowViewModel(
        ILauncherRuntime runtime,
        DesktopShellService desktopShellService,
        CreateProfileDialogService createProfileDialogService)
    {
        _runtime = runtime;
        _desktopShellService = desktopShellService;
        _createProfileDialogService = createProfileDialogService;
        Title = "Project Zomboid Server Launcher";
        Subtitle = "Desktop control for the integrated PZServerLauncher runtime.";
        HostSummary = "Starting integrated runtime...";
        StatusMessage = "Starting up...";

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateStarterProfileCommand = new AsyncRelayCommand(CreateStarterProfileAsync);
        DiscoverImportsCommand = new AsyncRelayCommand(DiscoverImportsAsync);
        ImportCandidateCommand = new AsyncRelayCommand<ImportCandidateViewModel>(ImportCandidateAsync);
        InstallCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _runtime.InstallAsync, "Install"));
        UpdateCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _runtime.UpdateAsync, "Update"));
        StartCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _runtime.StartAsync, "Start"));
        StopCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _runtime.StopAsync, "Stop"));
        RestartCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _runtime.RestartAsync, "Restart"));
        BackupCommand = new AsyncRelayCommand<ProfileCardViewModel>(profile => RunProfileActionAsync(profile, _runtime.BackupAsync, "Backup"));
        RestoreCommand = new AsyncRelayCommand<ProfileCardViewModel>(RestoreLatestBackupAsync);
        SaveCommonConfigCommand = new AsyncRelayCommand<ProfileCardViewModel>(SaveCommonConfigAsync);
        ScanWorkshopCommand = new AsyncRelayCommand<ProfileCardViewModel>(ScanWorkshopAsync);
        LoadRawConfigCommand = new AsyncRelayCommand<ProfileCardViewModel>(LoadRawConfigAsync);
        SaveRawConfigCommand = new AsyncRelayCommand<ProfileCardViewModel>(SaveRawConfigAsync);
        SaveHostSettingsCommand = new AsyncRelayCommand(SaveHostSettingsAsync);
        StopHostCommand = new AsyncRelayCommand(() => StopHostAsync(stopRunningServers: false));
        StopAllAndHostCommand = new AsyncRelayCommand(() => StopHostAsync(stopRunningServers: true));
        CheckLauncherUpdateCommand = new AsyncRelayCommand(CheckLauncherUpdateAsync);
        OpenLauncherReleasePageCommand = new RelayCommand(OpenLauncherReleasePage);
        ExitDesktopCommand = new RelayCommand(() => _desktopShellService.ExitDesktop());

        _runtime.StatusChanged += OnStatusChangedAsync;
        _runtime.JobChanged += OnJobChangedAsync;
        _runtime.LogLineReceived += OnLogLineReceivedAsync;

        _ = InitializeAsync();
    }

    public string Title { get; }

    public string Subtitle { get; }

    [ObservableProperty]
    private string hostSummary;

    [ObservableProperty]
    private string statusMessage;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hostStartWithWindows;

    [ObservableProperty]
    private LauncherUpdateState launcherUpdateState = LauncherUpdateState.Unavailable;

    [ObservableProperty]
    private string launcherCurrentVersion = "Unknown";

    [ObservableProperty]
    private string launcherLatestVersion = "Unavailable";

    [ObservableProperty]
    private string launcherReleaseTitle = "Latest stable release metadata is not available yet.";

    [ObservableProperty]
    private string launcherReleasePublishedLabel = "Not checked yet";

    [ObservableProperty]
    private string launcherLastCheckedLabel = "Not checked yet";

    [ObservableProperty]
    private string launcherUpdateStatusMessage = "Checking for launcher updates...";

    [ObservableProperty]
    private string launcherReleasePageUrl = string.Empty;

    [ObservableProperty]
    private bool isLauncherUpdateCheckRunning;

    public ObservableCollection<ProfileCardViewModel> Profiles { get; } = [];

    public ObservableCollection<ImportCandidateViewModel> ImportCandidates { get; } = [];

    public ObservableCollection<ShellItemViewModel> RecentJobs { get; } = [];

    public ObservableCollection<OperationJob> RecentOperationJobs { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand CreateStarterProfileCommand { get; }

    public IAsyncRelayCommand DiscoverImportsCommand { get; }

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

    public IAsyncRelayCommand CheckLauncherUpdateCommand { get; }

    public IRelayCommand OpenLauncherReleasePageCommand { get; }

    public IRelayCommand ExitDesktopCommand { get; }

    public string LauncherUpdateStateLabel => LauncherUpdateState switch
    {
        LauncherUpdateState.UpdateAvailable => "Update available",
        LauncherUpdateState.UpToDate => "Up to date",
        _ => "Check unavailable",
    };

    public bool HasLauncherReleasePage => !string.IsNullOrWhiteSpace(LauncherReleasePageUrl);

    public event EventHandler<WorkspaceNavigationRequest>? WorkspaceNavigationRequested;

    private async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await RunBusyAsync(RefreshCoreAsync, "Refreshing runtime state...");
    }

    private async Task RefreshCoreAsync()
    {
        var snapshot = await _runtime.LoadSnapshotAsync();
        _loadedHostSettings = snapshot.HostInfo.Settings;
        HostSummary = $"Integrated runtime online | {snapshot.HostInfo.Health.RunningProfileCount} running | {snapshot.Profiles.Count} managed";
        HostStartWithWindows = snapshot.HostInfo.Settings.StartHostWithWindows;
        ApplyLauncherUpdateStatus(await _runtime.GetLauncherUpdateStatusAsync(cancellationToken: CancellationToken.None));

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
        RecentOperationJobs.Clear();
        foreach (var job in snapshot.Jobs)
        {
            RecentOperationJobs.Add(job);
            RecentJobs.Add(new ShellItemViewModel(
                job.JobId.ToString("N"),
                $"{job.Kind} - {job.Status}",
                job.Detail ?? job.Summary));
        }

        if (Profiles.Count == 0 && ImportCandidates.Count == 0 && !_attemptedInitialImportDiscovery)
        {
            _attemptedInitialImportDiscovery = true;
            await DiscoverImportsCoreAsync(updateStatusMessage: false);
        }

        StatusMessage = Profiles.Count == 0
            ? ImportCandidates.Count > 0
                ? $"Found {ImportCandidates.Count} local server candidate(s). Adopt one or create a new managed server."
                : "The integrated runtime is online. Create a managed server or scan the machine for an existing local setup."
            : $"Loaded {Profiles.Count} profile(s).";

    }

    private async Task CreateStarterProfileAsync()
    {
        var request = await _createProfileDialogService.ShowAsync(BuildCreateProfileReservations());
        if (request is null)
        {
            return;
        }

        var previewProfile = ServerProfileFactory.CreateStarterProfile(
            request.DisplayName,
            ServerProfileFactory.FindNextAvailableStarterPort(
                request.DefaultPort,
                BuildReservedPorts()),
            Profiles.Select(profile => profile.ProfileId),
            preferredMemoryInGigabytes: request.PreferredMemoryInGigabytes);

        await RunBusyAsync(async () =>
        {
            var createdProfile = await _runtime.CreateStarterProfileAsync(
                request.DisplayName,
                request.DefaultPort,
                request.PreferredMemoryInGigabytes,
                request.MaxPlayers)
                ?? throw new InvalidOperationException("Profile creation did not return the new profile.");
            await RefreshCoreAsync();
            StatusMessage = createdProfile.DefaultPort == request.DefaultPort
                ? $"Created {createdProfile.DisplayName} on {createdProfile.DefaultPort}/{createdProfile.UdpPort}/{createdProfile.RconPort} with {request.PreferredMemoryInGigabytes} GB and {request.MaxPlayers} max players."
                : $"Created {createdProfile.DisplayName} on {createdProfile.DefaultPort}/{createdProfile.UdpPort}/{createdProfile.RconPort} with {request.PreferredMemoryInGigabytes} GB and {request.MaxPlayers} max players after skipping ports already reserved by other profiles.";
            RequestProfileNavigation(createdProfile.ProfileId);
        }, $"Creating {previewProfile.DisplayName}...");
    }

    private async Task DiscoverImportsAsync()
    {
        await RunBusyAsync(async () =>
        {
            await DiscoverImportsCoreAsync(updateStatusMessage: true);
        }, "Scanning for existing local servers...");
    }

    private async Task ImportCandidateAsync(ImportCandidateViewModel? candidate)
    {
        if (candidate is null || candidate.IsAlreadyImported)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var importedProfile = await _runtime.ImportLocalCandidateAsync(candidate.CandidateId, CancellationToken.None);
            await RefreshCoreAsync();
            await DiscoverImportsCoreAsync(updateStatusMessage: false);
            StatusMessage = $"Imported {candidate.DisplayName}.";
            if (importedProfile is not null)
            {
                RequestProfileNavigation(importedProfile.ProfileId);
            }
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

        if (IsMaintenanceAction(actionName) && HasActiveLifecycleJob(profile.ProfileId))
        {
            StatusMessage = $"Install or update is already running for {profile.DisplayName}. SteamCMD can take a few minutes on first install, so let the current job finish before trying again.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await action(profile.ProfileId, CancellationToken.None);
            StatusMessage = result?.Message ?? $"{actionName} completed.";
            await RefreshCoreAsync();
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
            var result = await _runtime.RestoreAsync(profile.ProfileId, profile.LastBackup, restartAfterRestore: true, CancellationToken.None);
            StatusMessage = result?.Message ?? "Restore queued.";
            await RefreshCoreAsync();
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
            await _runtime.UpdateCommonConfigAsync(profile.ProfileId, commonConfig!, CancellationToken.None);
            await RefreshCoreAsync();
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
            var result = await _runtime.ScanWorkshopAsync(profile.ProfileId, CancellationToken.None);
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
            var result = await _runtime.GetRawConfigAsync(profile.ProfileId, profile.SelectedRawConfigKind.Kind, CancellationToken.None);
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

            var result = await _runtime.SaveRawConfigAsync(profile.ProfileId, profile.SelectedRawConfigKind.Kind, payload, CancellationToken.None);
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
            var current = _loadedHostSettings ?? await _runtime.GetHostSettingsAsync(CancellationToken.None)
                ?? throw new InvalidOperationException("Host settings could not be loaded.");

            var updated = await _runtime.UpdateHostSettingsAsync(current with
            {
                StartHostWithWindows = HostStartWithWindows,
            }, CancellationToken.None);

            _loadedHostSettings = updated ?? current with { StartHostWithWindows = HostStartWithWindows };
            HostStartWithWindows = _loadedHostSettings.StartHostWithWindows;
            StatusMessage = HostStartWithWindows
                ? "The launcher will start with Windows for this user."
                : "The launcher will no longer start with Windows.";
        }, "Saving host settings...");
    }

    private async Task StopHostAsync(bool stopRunningServers)
    {
        await RunBusyAsync(async () =>
        {
            var result = await _runtime.StopRuntimeAsync(stopRunningServers, CancellationToken.None);

            HostSummary = "Integrated runtime stopped. Use Refresh to start it again.";
            StatusMessage = result?.Message ?? "Integrated runtime shutdown requested.";
        }, stopRunningServers ? "Stopping all servers and the integrated runtime..." : "Stopping the integrated runtime...");
    }

    private async Task CheckLauncherUpdateAsync()
    {
        if (IsLauncherUpdateCheckRunning)
        {
            return;
        }

        try
        {
            IsLauncherUpdateCheckRunning = true;
            StatusMessage = "Checking for launcher updates...";
            ApplyLauncherUpdateStatus(await _runtime.GetLauncherUpdateStatusAsync(forceRefresh: true, CancellationToken.None));
            StatusMessage = LauncherUpdateStatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLauncherUpdateCheckRunning = false;
        }
    }

    private void OpenLauncherReleasePage()
    {
        if (!HasLauncherReleasePage)
        {
            return;
        }

        _desktopShellService.OpenExternalUrl(LauncherReleasePageUrl);
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
            var existingOperationJobIndex = RecentOperationJobs
                .Select((value, index) => new { value, index })
                .FirstOrDefault(entry => entry.value.JobId == job.JobId)?.index;
            if (existingOperationJobIndex is int operationIndex)
            {
                RecentOperationJobs[operationIndex] = job;
            }
            else
            {
                RecentOperationJobs.Insert(0, job);
                while (RecentOperationJobs.Count > 20)
                {
                    RecentOperationJobs.RemoveAt(RecentOperationJobs.Count - 1);
                }
            }

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

    private void ApplyLauncherUpdateStatus(LauncherUpdateStatusDto? status)
    {
        if (status is null)
        {
            LauncherUpdateState = LauncherUpdateState.Unavailable;
            LauncherCurrentVersion = "Unknown";
            LauncherLatestVersion = "Unavailable";
            LauncherReleaseTitle = "Latest stable release metadata is not available yet.";
            LauncherReleasePublishedLabel = "Unavailable";
            LauncherLastCheckedLabel = "Unavailable";
            LauncherUpdateStatusMessage = "Unable to load launcher update status.";
            LauncherReleasePageUrl = string.Empty;
            return;
        }

        LauncherUpdateState = status.State;
        LauncherCurrentVersion = status.CurrentVersion;
        LauncherLatestVersion = string.IsNullOrWhiteSpace(status.LatestVersion) ? "Unavailable" : status.LatestVersion;
        LauncherReleaseTitle = string.IsNullOrWhiteSpace(status.ReleaseTitle)
            ? "Latest stable release metadata is not available yet."
            : status.ReleaseTitle.Trim();
        LauncherReleasePublishedLabel = status.PublishedAtUtc?.ToLocalTime().ToString("g") ?? "Unavailable";
        LauncherLastCheckedLabel = status.CheckedAtUtc.ToLocalTime().ToString("g");
        LauncherUpdateStatusMessage = status.StatusMessage;
        LauncherReleasePageUrl = status.ReleasePageUrl?.Trim() ?? string.Empty;
    }

    private async Task DiscoverImportsCoreAsync(bool updateStatusMessage)
    {
        var candidates = await _runtime.DiscoverLocalImportsAsync(CancellationToken.None) ?? [];
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

        if (!updateStatusMessage)
        {
            return;
        }

        StatusMessage = ImportCandidates.Count == 0
            ? "No existing local Zomboid server configs were found."
            : $"Found {ImportCandidates.Count} import candidate(s).";
    }

    private void RequestProfileNavigation(string profileId)
    {
        var profile = Profiles.FirstOrDefault(candidate => string.Equals(candidate.ProfileId, profileId, StringComparison.Ordinal));
        if (profile is null)
        {
            return;
        }

        var nextProfilePage = profile.IsInstallDetected
            ? ProfileWorkspacePageIds.Overview
            : ProfileWorkspacePageIds.InstallAndUpdate;

        WorkspaceNavigationRequested?.Invoke(
            this,
            new WorkspaceNavigationRequest(
                WorkspacePageIds.Profiles,
                profile.ProfileId,
                nextProfilePage));
    }

    private static string FormatWorkshopSummary(WorkshopPreset preset) =>
        $"{preset.WorkshopItemIds.Count} workshop / {preset.EnabledModIds.Count} mods / {preset.MapFolders.Count} maps";

    private IReadOnlyList<CreateProfilePortReservation> BuildCreateProfileReservations() =>
        Profiles
            .Select(profile => new CreateProfilePortReservation(
                profile.ProfileId,
                profile.DisplayName,
                ParsePort(profile.EditableDefaultPort),
                ParsePort(profile.EditableUdpPort),
                ParsePort(profile.EditableRconPort)))
            .ToArray();

    private IEnumerable<int> BuildReservedPorts() =>
        BuildCreateProfileReservations()
            .SelectMany(profile => profile.ReservedPorts);

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
            var generalTask = _runtime.GetSettingsPageAsync(profileId, ProfileWorkspacePageIds.General, CancellationToken.None);
            var networkTask = _runtime.GetSettingsPageAsync(profileId, ProfileWorkspacePageIds.NetworkAndAdmin, CancellationToken.None);
            var sandboxTask = _runtime.GetSettingsPageAsync(profileId, ProfileWorkspacePageIds.Sandbox, CancellationToken.None);
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

    private static int ParsePort(string value) =>
        int.TryParse(value, out var port)
            ? port
            : 0;

    private bool HasActiveLifecycleJob(string profileId) =>
        RecentOperationJobs.Any(job =>
            string.Equals(job.ProfileId, profileId, StringComparison.OrdinalIgnoreCase) &&
            job.Kind is OperationJobKind.Install or OperationJobKind.Update &&
            job.Status is OperationJobStatus.Queued or OperationJobStatus.Running);

    private static bool IsMaintenanceAction(string actionName) =>
        string.Equals(actionName, "Install", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actionName, "Update", StringComparison.OrdinalIgnoreCase);

    partial void OnLauncherUpdateStateChanged(LauncherUpdateState value)
    {
        OnPropertyChanged(nameof(LauncherUpdateStateLabel));
    }

    partial void OnLauncherReleasePageUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasLauncherReleasePage));
    }
}
