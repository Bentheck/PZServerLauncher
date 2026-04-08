using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public sealed class OverviewWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private int _loadVersion;
    private string _communitySummary = "Select a profile to load community posture.";
    private string _serverRulesSummary = "No server rules loaded.";
    private string _networkPostureSummary = "No network posture loaded.";
    private string _worldSnapshotSummary = "No world snapshot loaded.";
    private string _welcomeMessageSummary = "No welcome message configured yet.";

    public OverviewWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            "overview",
            "Overview",
            "Runtime state, backups, and quick actions for the selected profile.",
            "Overview has no unsaved draft.",
            legacy,
            ["Runtime state", "Latest log", "Backup summary", "Quick actions"])
    {
        _hostApiClient = hostApiClient;
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

        try
        {
            var generalTask = _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.General);
            var networkTask = _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.NetworkAndAdmin);
            var sandboxTask = _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.Sandbox);
            await Task.WhenAll(generalTask, networkTask, sandboxTask);

            if (loadVersion != _loadVersion || SelectedProfile?.ProfileId != profile.ProfileId)
            {
                return;
            }

            var generalValues = generalTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            var networkValues = networkTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            var sandboxValues = sandboxTask.Result?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);

            SetStructuredSummaries(
                BuildCommunitySummary(profile, generalValues),
                BuildServerRulesSummary(generalValues),
                BuildNetworkSummary(networkValues),
                BuildWorldSnapshotSummary(sandboxValues),
                BuildWelcomeMessageSummary(generalValues));
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

    private static string BuildCommunitySummary(ProfileCardViewModel profile, IReadOnlyDictionary<string, string?> values)
    {
        var publicName = GetValue(values, ".server.public-name");
        var maxPlayers = GetValue(values, ".server.max-players", "32");
        var isPublic = ParseBool(values, ".server.public");
        var isOpen = ParseBool(values, ".server.open");
        var pvp = ParseBool(values, ".server.pvp", true);

        return $"{(string.IsNullOrWhiteSpace(publicName) ? profile.DisplayName : publicName)} | {maxPlayers} slots | {(isPublic ? "public listing on" : "private listing")} | {(isOpen ? "open access" : "password-gated")} | PvP {(pvp ? "on" : "off")}.";
    }

    private static string BuildServerRulesSummary(IReadOnlyDictionary<string, string?> values)
    {
        var sleepAllowed = ParseBool(values, ".server.sleep-allowed");
        var sleepNeeded = ParseBool(values, ".server.sleep-needed");
        var playerSafehouse = ParseBool(values, ".server.player-safehouse");
        var factionEnabled = ParseBool(values, ".server.faction-enabled");
        var tradeUi = ParseBool(values, ".server.allow-trade-ui");
        var noFire = ParseBool(values, ".server.no-fire");

        return $"Sleep {(sleepAllowed ? (sleepNeeded ? "required" : "allowed") : "disabled")} | safehouses {(playerSafehouse ? "enabled" : "off")} | factions {(factionEnabled ? "enabled" : "off")} | trade UI {(tradeUi ? "enabled" : "off")} | fire spread {(noFire ? "disabled" : "enabled")}.";
    }

    private static string BuildNetworkSummary(IReadOnlyDictionary<string, string?> values)
    {
        var bindIp = GetValue(values, ".network.bind-ip", "default bind");
        var steamVac = ParseBool(values, ".network.steam-vac", true);
        var autoWhitelist = ParseBool(values, ".network.auto-whitelist");
        var safetySystem = ParseBool(values, ".network.safety-system", true);
        var voiceEnabled = ParseBool(values, ".network.voice-enabled", true);
        var voice3d = ParseBool(values, ".network.voice-3d", true);
        var voiceMin = GetValue(values, ".network.voice-min-distance", "10");
        var voiceMax = GetValue(values, ".network.voice-max-distance", "100");

        var voiceSummary = voiceEnabled
            ? voice3d
                ? $"3D voice {voiceMin}-{voiceMax}"
                : "global voice"
            : "voice disabled";

        return $"Bind {bindIp} | VAC {(steamVac ? "on" : "off")} | whitelist {(autoWhitelist ? "auto-create" : "manual")} | safety {(safetySystem ? "enabled" : "off")} | {voiceSummary}.";
    }

    private static string BuildWorldSnapshotSummary(IReadOnlyDictionary<string, string?> values)
    {
        var zombies = GetValue(values, ".sandbox.zombies", "4");
        var dayLength = GetValue(values, ".sandbox.day-length", "3");
        var waterShutoff = GetValue(values, ".sandbox.water-shut-modifier", "500");
        var electricityShutoff = GetValue(values, ".sandbox.electricity-shut-modifier", "480");
        var lootRespawn = GetValue(values, ".sandbox.loot-respawn", "2");
        var starterKit = ParseBool(values, ".sandbox.starter-kit");
        var nutrition = ParseBool(values, ".sandbox.nutrition");

        return $"Zombies {zombies} | day length {dayLength} | water shutoff {waterShutoff} days | electricity shutoff {electricityShutoff} days | loot respawn {lootRespawn} | starter kit {(starterKit ? "on" : "off")} | nutrition {(nutrition ? "on" : "off")}.";
    }

    private static string BuildWelcomeMessageSummary(IReadOnlyDictionary<string, string?> values)
    {
        var message = GetValue(values, ".server.welcome-message");
        if (string.IsNullOrWhiteSpace(message))
        {
            return "No welcome message configured yet.";
        }

        var singleLine = message.ReplaceLineEndings(" ").Replace("  ", " ", StringComparison.Ordinal).Trim();
        return $"Welcome: {singleLine}";
    }

    private static string GetValue(IReadOnlyDictionary<string, string?> values, string suffix, string fallback = "")
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? fallback : values[key] ?? fallback;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string?> values, string suffix, bool fallback = false)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is not null && bool.TryParse(values[key], out var parsed) ? parsed : fallback;
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
        OnPropertyChanged(nameof(CommunitySummary));
        OnPropertyChanged(nameof(ServerRulesSummary));
        OnPropertyChanged(nameof(NetworkPostureSummary));
        OnPropertyChanged(nameof(WorldSnapshotSummary));
        OnPropertyChanged(nameof(WelcomeMessageSummary));
        OnPropertyChanged(nameof(OperatorGuidance));
    }
}
