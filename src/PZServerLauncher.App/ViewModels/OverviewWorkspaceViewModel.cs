using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.App.ViewModels;

public sealed class OverviewWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private readonly RuntimeEventStream _runtimeEventStream;
    private int _loadVersion;
    private string _communitySummary = "Select a profile to load community posture.";
    private string _serverRulesSummary = "No server rules loaded.";
    private string _networkPostureSummary = "No network posture loaded.";
    private string _worldSnapshotSummary = "No world snapshot loaded.";
    private string _welcomeMessageSummary = "No welcome message configured yet.";
    private ProfileLiveOperationsSnapshot? _liveOperations;

    public OverviewWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient, RuntimeEventStream runtimeEventStream)
        : base(
            "overview",
            "Overview",
            "Runtime state, backups, and quick actions for the selected profile.",
            "Overview has no unsaved draft.",
            legacy,
            ["Runtime state", "Latest log", "Backup summary", "Quick actions"])
    {
        _hostApiClient = hostApiClient;
        _runtimeEventStream = runtimeEventStream;
        InstallCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.InstallCommand));
        UpdateCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.UpdateCommand));
        StartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StartCommand));
        StopCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StopCommand));
        RestartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.RestartCommand));
        BackupCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.BackupCommand));
        RestoreCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.RestoreCommand));
        _runtimeEventStream.LiveOperationsChanged += OnLiveOperationsChangedAsync;
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

    public string LivePlayerSummary => _liveOperations is null
        ? "Player roster unavailable."
        : _liveOperations.ConnectedPlayers.Count == 0
            ? "No players inferred online right now."
            : $"{_liveOperations.ConnectedPlayers.Count} player(s) inferred online from the live log stream.";

    public string LivePlayerRosterSummary => _liveOperations is null
        ? "No live roster loaded yet."
        : _liveOperations.ConnectedPlayers.Count == 0
            ? "No active player roster has been inferred yet."
            : string.Join(", ", _liveOperations.ConnectedPlayers.Select(player => player.UserName));

    public string PlayerActivitySummary => _liveOperations is null
        ? "No recent player activity loaded."
        : _liveOperations.RecentPlayerSignals.Count == 0
            ? "Join and leave signals have not been inferred from recent runtime output yet."
            : $"{_liveOperations.RecentPlayerSignals[0].UserName} {_liveOperations.RecentPlayerSignals[0].Activity.ToLowerInvariant()} at {_liveOperations.RecentPlayerSignals[0].TimestampUtc:HH:mm:ss} UTC.";

    public string OperatorActionSummary => _liveOperations is null
        ? "No recent broadcast or raw console action has been loaded yet."
        : _liveOperations.RecentOperatorActions.Count == 0
            ? "No recent broadcast or raw console action has been sent from the launcher."
            : _liveOperations.RecentOperatorActions[0].Summary;

    public string CommunitySummary => _communitySummary;

    public string ServerRulesSummary => _serverRulesSummary;

    public string NetworkPostureSummary => _networkPostureSummary;

    public string WorldSnapshotSummary => _worldSnapshotSummary;

    public string WelcomeMessageSummary => _welcomeMessageSummary;

    public string OperatorGuidance => SelectedProfile is null
        ? "Pick or import a profile to start managing a server."
        : !Directory.Exists(SelectedProfile.InstallDirectory)
            ? "Install this branch first, then return here to launch or configure the server."
                : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "The server is currently live. Use Logs, Backups, and Mods & Maps for the most common active-admin tasks."
                : "The server is installed and idle. Review settings, capture a backup, then start it from this page or Install & Update.";

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = RefreshStructuredOverviewAsync(profile);
        Notify();
    }

    private async Task ExecuteProfileCommandAsync(IAsyncRelayCommand<ProfileCardViewModel> command)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await command.ExecuteAsync(SelectedProfile);
        await RefreshStructuredOverviewAsync(SelectedProfile);
        Notify();
    }

    private async Task RefreshStructuredOverviewAsync(ProfileCardViewModel? profile)
    {
        var loadVersion = Interlocked.Increment(ref _loadVersion);

        if (profile is null)
        {
            SetStructuredSummaries(
                "Select a profile to load community posture.",
                "No server rules loaded.",
                "No network posture loaded.",
                "No world snapshot loaded.",
                "No welcome message configured yet.");
            return;
        }

        SetStructuredSummaries(
            "Loading structured community posture...",
            "Loading server rules...",
            "Loading network posture...",
            "Loading world snapshot...",
            "Loading welcome message...");
        _liveOperations = new ProfileLiveOperationsSnapshot(profile.ProfileId, [], [], [], true, null);

        try
        {
            var generalTask = _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.General);
            var networkTask = _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.NetworkAndAdmin);
            var sandboxTask = _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.Sandbox);
            var liveOperationsTask = _hostApiClient.GetLiveOperationsAsync(profile.ProfileId);
            await Task.WhenAll(generalTask, networkTask, sandboxTask, liveOperationsTask);

            if (loadVersion != _loadVersion || SelectedProfile?.ProfileId != profile.ProfileId)
            {
                return;
            }

            var generalValues = generalTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            var networkValues = networkTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            var sandboxValues = sandboxTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            _liveOperations = liveOperationsTask.Result ?? _liveOperations;

            var posture = ProjectZomboidProfilePostureSummaryBuilder.Build(
                profile.DisplayName,
                generalValues,
                networkValues,
                sandboxValues);

            SetStructuredSummaries(
                posture.CommunitySummary,
                posture.ServerRulesSummary,
                posture.NetworkSummary,
                posture.WorldSummary,
                posture.WelcomeSummary);
            OnPropertyChanged(nameof(LivePlayerSummary));
            OnPropertyChanged(nameof(LivePlayerRosterSummary));
            OnPropertyChanged(nameof(PlayerActivitySummary));
            OnPropertyChanged(nameof(OperatorActionSummary));
        }
        catch
        {
            if (loadVersion != _loadVersion || SelectedProfile?.ProfileId != profile.ProfileId)
            {
                return;
            }

            SetStructuredSummaries(
                "Structured community posture could not be loaded yet.",
                "Server rules summary is temporarily unavailable.",
                "Network posture summary is temporarily unavailable.",
                "World snapshot is temporarily unavailable.",
                "Welcome message summary is temporarily unavailable.");
        }
    }

    private Task OnLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot)
    {
        if (SelectedProfile is null || !string.Equals(SelectedProfile.ProfileId, snapshot.ProfileId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        _liveOperations = snapshot;
        OnPropertyChanged(nameof(LivePlayerSummary));
        OnPropertyChanged(nameof(LivePlayerRosterSummary));
        OnPropertyChanged(nameof(PlayerActivitySummary));
        OnPropertyChanged(nameof(OperatorActionSummary));
        return Task.CompletedTask;
    }

    private void SetStructuredSummaries(
        string communitySummary,
        string serverRulesSummary,
        string networkPostureSummary,
        string worldSnapshotSummary,
        string welcomeMessageSummary)
    {
        _communitySummary = communitySummary;
        _serverRulesSummary = serverRulesSummary;
        _networkPostureSummary = networkPostureSummary;
        _worldSnapshotSummary = worldSnapshotSummary;
        _welcomeMessageSummary = welcomeMessageSummary;
        OnPropertyChanged(nameof(CommunitySummary));
        OnPropertyChanged(nameof(ServerRulesSummary));
        OnPropertyChanged(nameof(NetworkPostureSummary));
        OnPropertyChanged(nameof(WorldSnapshotSummary));
        OnPropertyChanged(nameof(WelcomeMessageSummary));
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
        OnPropertyChanged(nameof(LivePlayerSummary));
        OnPropertyChanged(nameof(LivePlayerRosterSummary));
        OnPropertyChanged(nameof(PlayerActivitySummary));
        OnPropertyChanged(nameof(OperatorActionSummary));
        OnPropertyChanged(nameof(CommunitySummary));
        OnPropertyChanged(nameof(ServerRulesSummary));
        OnPropertyChanged(nameof(NetworkPostureSummary));
        OnPropertyChanged(nameof(WorldSnapshotSummary));
        OnPropertyChanged(nameof(WelcomeMessageSummary));
        OnPropertyChanged(nameof(OperatorGuidance));
    }
}
