using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class LogsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private static readonly ProjectZomboidLogPostureSummary EmptySummary = new(
        "No buffered lines are available yet. Select a profile to inspect runtime output.",
        "Latest signal: no runtime output captured yet.",
        "No runtime signals are buffered yet.",
        "Pick a profile, then start or reload to watch runtime output.",
        "Runtime window: no status is available yet.",
        "No player activity is available yet.",
        "No operator commands have been recorded yet.",
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        [],
        []);

    private readonly ILauncherRuntime _runtime;
    private ServerRuntimeStatus? _runtimeStatus;
    private ProfileLiveOperationsSnapshot? _liveOperations;

    public LogsWorkspaceViewModel(
        MainWindowViewModel legacy,
        ILauncherRuntime runtime)
        : base(
            ProfileWorkspacePageIds.Logs,
            "Logs",
            "Recent runtime output, inferred live player activity, and operator console actions for the selected profile.",
            "Logs are in sync.",
            legacy,
            ["Recent log buffer", "Live line feed", "Player roster", "Operator commands"])
    {
        _runtime = runtime;
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        SendBroadcastCommand = new AsyncRelayCommand(SendBroadcastAsync);
        SendConsoleCommand = new AsyncRelayCommand(SendConsoleCommandAsync);
        ListPlayersCommand = new AsyncRelayCommand(() => SendQuickCommandAsync("players", "Requested current player list."));
        SaveWorldCommand = new AsyncRelayCommand(() => SendQuickCommandAsync("save", "Requested world save."));
        ReloadOptionsCommand = new AsyncRelayCommand(() => SendQuickCommandAsync("reloadoptions", "Requested option reload."));
        KickPlayerCommand = new AsyncRelayCommand<ConnectedPlayerRowViewModel>(KickPlayerAsync);
        BanPlayerCommand = new AsyncRelayCommand<ConnectedPlayerRowViewModel>(BanPlayerAsync);
        WhitelistPlayerCommand = new AsyncRelayCommand<ConnectedPlayerRowViewModel>(WhitelistPlayerAsync);
        _runtime.LogLineReceived += OnLogLineReceivedAsync;
        _runtime.StatusChanged += OnStatusChangedAsync;
        _runtime.LiveOperationsChanged += OnLiveOperationsChangedAsync;
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to inspect runtime output, roster posture, and live operator controls."
        : $"Recent runtime output, inferred player activity, and live operator controls for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string ConsoleHeroTitle => SelectedProfile is null
        ? "Live Console"
        : $"{SelectedProfile.DisplayName} Live Console";

    public string ConsoleHeroCopy => SelectedProfile is null
        ? "Select a profile to inspect buffered runtime output, roster posture, and the live operator controls."
        : $"Keep this console open to watch buffered runtime output, roster posture, and launcher-issued commands for {SelectedProfile.DisplayName}.";

    public ObservableCollection<string> LogLines { get; } = [];

    public ObservableCollection<ConnectedPlayerRowViewModel> ConnectedPlayers { get; } = [];

    public ObservableCollection<string> RecentPlayerSignals { get; } = [];

    public ObservableCollection<string> RecentOperatorActions { get; } = [];

    public bool HasLogs => LogLines.Count > 0;

    public bool HasNoLogs => LogLines.Count == 0;

    public bool HasConnectedPlayers => ConnectedPlayers.Count > 0;

    public bool HasNoConnectedPlayers => ConnectedPlayers.Count == 0;

    public bool HasPlayerSignals => RecentPlayerSignals.Count > 0;

    public bool HasNoPlayerSignals => RecentPlayerSignals.Count == 0;

    public bool HasOperatorActions => RecentOperatorActions.Count > 0;

    public bool HasNoOperatorActions => RecentOperatorActions.Count == 0;

    public string LogStreamSummary => SelectedProfile is null
        ? "Choose a profile to inspect server output."
        : CurrentSummary.BufferSummary;

    public string BufferCountSummary => SelectedProfile is null
        ? "No buffer count available."
        : CurrentSummary.BufferedLineCount == 0
            ? "No buffered lines yet"
            : $"{CurrentSummary.BufferedLineCount} buffered line(s)";

    public string FeedHealthHeadline => SelectedProfile is null
        ? "No profile"
        : CurrentConsoleSummary.FeedHeadline;

    public string FeedPostureSummary => SelectedProfile is null
        ? "No live feed is available yet."
        : CurrentSummary.SignalPostureSummary;

    public string SignalCountSummary => SelectedProfile is null
        ? "No signal count available."
        : CurrentSummary.WarningSignalCount + CurrentSummary.ErrorSignalCount == 0
            ? "No warnings or errors buffered"
            : $"{CurrentSummary.ErrorSignalCount} error(s) | {CurrentSummary.WarningSignalCount} warning(s)";

    public string RuntimeGuidance => SelectedProfile is null
        ? "No runtime guidance available."
        : CurrentConsoleSummary.OperatorSummary;

    public string LatestLineSummary => CurrentSummary.LatestSignalSummary;

    public string LatestSignalLabel => SelectedProfile is null
        ? "Latest signal unavailable"
        : CurrentSummary.LatestSignalSummary;

    public string OperatorNextStep => SelectedProfile is null
        ? "Pick a profile, then start or reload to watch runtime output."
        : CurrentSummary.OperatorFocusSummary;

    public string ConsoleStatusSummary => SelectedProfile is null
        ? "No console context loaded."
        : $"{LatestRuntimeState} | {BufferCountSummary} | {ActivePlayerCountSummary}";

    public string RuntimeWindowSummary => SelectedProfile is null
        ? "Runtime window: no status is available yet."
        : CurrentSummary.RuntimeWindowSummary;

    public string PlayerActivityHeadline => _liveOperations is null
        ? "No roster sample"
        : CurrentConsoleSummary.RosterHeadline;

    public string ActivePlayerCountSummary => _liveOperations is null
        ? "No live roster sampled"
        : _liveOperations.ConnectedPlayers.Count == 0
            ? _liveOperations.IsRosterInferredFromLogs
                ? "Roster inferred, but nobody is online"
                : "No players inferred online"
            : $"{_liveOperations.ConnectedPlayers.Count} player(s) inferred online";

    public string RosterPostureSummary => _liveOperations is null
        ? "No live roster signal has been captured yet."
        : _liveOperations.IsRosterInferredFromLogs
            ? "The current roster is inferred from recent runtime signals."
            : "The current roster has not been inferred from the buffered runtime feed yet.";

    public string PlayerModerationSummary => SelectedProfile is null
        ? "Pick a profile before using moderation actions."
        : CanSendCommands
            ? "Kick, ban, or whitelist directly from the inferred roster when the runtime is live."
            : "Targeted moderation actions unlock only while the runtime is live.";

    public string PlayerSignalSummary => _liveOperations is null
        ? "No player activity inferred yet."
        : _liveOperations.RecentPlayerSignals.Count == 0
            ? "No player join or leave signals have been inferred from the recent live buffer yet."
            : $"Recent player activity is inferred from {_liveOperations.RecentPlayerSignals.Count} live signal(s).";

    public string PlayerSignalCountSummary => _liveOperations is null
        ? "No player signals yet."
        : _liveOperations.RecentPlayerSignals.Count == 0
            ? "0 player signals"
            : $"{_liveOperations.RecentPlayerSignals.Count} player signal(s)";

    public string OperatorActionSummary => _liveOperations is null
        ? "No operator actions recorded yet."
        : _liveOperations.RecentOperatorActions.Count == 0
            ? "Broadcasts and raw console commands will appear here after they are sent."
            : _liveOperations.RecentOperatorActions[0].Summary;

    public string OperatorActionHeadline => SelectedProfile is null
        ? "No profile"
        : CurrentConsoleSummary.CommandHeadline;

    public string OperatorActionCountSummary => _liveOperations is null
        ? "No operator commands yet."
        : _liveOperations.RecentOperatorActions.Count == 0
            ? "0 operator commands"
            : $"{_liveOperations.RecentOperatorActions.Count} command(s) logged";

    public string OperatorCommandPosture => string.IsNullOrWhiteSpace(_runtimeStatus?.LastOperatorCommandSummary)
        ? "No recent launcher-driven broadcast or console command has been recorded."
        : _runtimeStatus.LastOperatorCommandSummary!;

    public string IncidentHeadline => SelectedProfile is null
        ? "No incident posture"
        : CurrentConsoleSummary.IncidentHeadline;

    public string LiveOpsOperatorSummary => SelectedProfile is null
        ? "Select a profile to review live operations."
        : CurrentConsoleSummary.OperatorSummary;

    public string LiveOpsTriageSummary => SelectedProfile is null
        ? "No triage guidance loaded."
        : CurrentConsoleSummary.TriageSummary;

    public string RosterSignalSummary => _liveOperations is null
        ? "No roster signal available."
        : _liveOperations.ConnectedPlayers.Count == 0
            ? "No connected players inferred"
            : string.Join(", ", _liveOperations.ConnectedPlayers.Select(player => $"{player.UserName}"));

    public string ConsoleModeSummary => SelectedProfile is null
        ? "No console mode loaded."
        : CanSendCommands
            ? "Live command mode unlocked"
            : "Monitor-only mode until the server is running";

    public string BroadcastGuidance => SelectedProfile is null
        ? "Select a profile to unlock live broadcast and console controls."
        : CanSendCommands
            ? "Send a broadcast, request the player list, save the world, or issue a raw console command while the server is live."
            : "The live console can reload and review signals while the server is stopped, but broadcasts and raw commands unlock only when the runtime is active.";

    public IReadOnlyList<string> LiveOpsChecklist => SelectedProfile is null
        ? []
        : CurrentConsoleSummary.Checklist;

    public bool CanSendCommands => SelectedProfile is not null &&
        string.Equals(LatestRuntimeState, ServerRuntimeState.Running.ToString(), StringComparison.OrdinalIgnoreCase);

    public IAsyncRelayCommand ReloadCommand { get; }

    public IAsyncRelayCommand SendBroadcastCommand { get; }

    public IAsyncRelayCommand SendConsoleCommand { get; }

    public IAsyncRelayCommand ListPlayersCommand { get; }

    public IAsyncRelayCommand SaveWorldCommand { get; }

    public IAsyncRelayCommand ReloadOptionsCommand { get; }

    public IAsyncRelayCommand<ConnectedPlayerRowViewModel> KickPlayerCommand { get; }

    public IAsyncRelayCommand<ConnectedPlayerRowViewModel> BanPlayerCommand { get; }

    public IAsyncRelayCommand<ConnectedPlayerRowViewModel> WhitelistPlayerCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load recent logs.";

    [ObservableProperty]
    private string latestRuntimeState = "Unknown";

    [ObservableProperty]
    private string broadcastMessage = string.Empty;

    [ObservableProperty]
    private string rawConsoleCommand = string.Empty;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        NotifyComputedState();
        _ = LoadAsync(profile);
    }

    public override async Task RefreshPageAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    public override Task SaveDraftAsync() => Task.CompletedTask;

    public override Task DiscardDraftAsync() => Task.CompletedTask;

    private async Task ReloadAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    private async Task LoadAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            Reset();
            return;
        }

        LoadStatus = $"Loading live console and ops for {profile.DisplayName}...";
        var statusTask = _runtime.GetStatusAsync(profile.ProfileId);
        var logsTask = _runtime.GetRecentLogsAsync(profile.ProfileId);
        var operationsTask = _runtime.GetLiveOperationsAsync(profile.ProfileId);
        await Task.WhenAll(statusTask, logsTask, operationsTask);

        _runtimeStatus = statusTask.Result
            ?? new ServerRuntimeStatus(profile.ProfileId, ServerRuntimeState.Stopped, null, null, null, null, profile.LatestLogLine);
        LatestRuntimeState = _runtimeStatus.State.ToString();

        LogLines.Clear();
        foreach (var line in logsTask.Result ?? [])
        {
            LogLines.Add(line);
        }

        ApplyLiveOperations(operationsTask.Result);

        LoadStatus = LogLines.Count == 0
            ? "No recent log lines are buffered yet. Start the server or wait for new runtime output."
            : $"Loaded {LogLines.Count} recent log line(s) for {profile.DisplayName}.";
        NotifyComputedState();
    }

    private async Task SendBroadcastAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(BroadcastMessage))
        {
            return;
        }

        try
        {
            var response = await _runtime.SendBroadcastAsync(SelectedProfile.ProfileId, BroadcastMessage);
            ApplyLiveOperations(response);
            LoadStatus = $"Broadcast queued for {SelectedProfile.DisplayName}.";
            BroadcastMessage = string.Empty;
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
            NotifyComputedState();
        }
    }

    private async Task SendConsoleCommandAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(RawConsoleCommand))
        {
            return;
        }

        await SendQuickCommandAsync(RawConsoleCommand, $"Console command queued for {SelectedProfile.DisplayName}.");
        RawConsoleCommand = string.Empty;
    }

    private async Task SendQuickCommandAsync(string command, string statusMessage)
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        try
        {
            var response = await _runtime.SendConsoleCommandAsync(SelectedProfile.ProfileId, command);
            ApplyLiveOperations(response);
            LoadStatus = statusMessage;
            NotifyComputedState();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
            NotifyComputedState();
        }
    }

    private Task KickPlayerAsync(ConnectedPlayerRowViewModel? player) =>
        SendTargetedCommandAsync("kickuser", player, "Queued kick command");

    private Task BanPlayerAsync(ConnectedPlayerRowViewModel? player) =>
        SendTargetedCommandAsync("banuser", player, "Queued ban command");

    private Task WhitelistPlayerAsync(ConnectedPlayerRowViewModel? player) =>
        SendTargetedCommandAsync("addusertowhitelist", player, "Queued whitelist command");

    private Task SendTargetedCommandAsync(string commandName, ConnectedPlayerRowViewModel? player, string actionLabel)
    {
        if (player is null)
        {
            return Task.CompletedTask;
        }

        var command = $"{commandName} {QuoteConsoleArgument(player.UserName)}";
        return SendQuickCommandAsync(command, $"{actionLabel} for {player.UserName}.");
    }

    private Task OnLogLineReceivedAsync(string profileId, string line)
    {
        if (SelectedProfile is null || !string.Equals(SelectedProfile.ProfileId, profileId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        LogLines.Add(line);
        while (LogLines.Count > 250)
        {
            LogLines.RemoveAt(0);
        }

        _runtimeStatus = (_runtimeStatus ?? new ServerRuntimeStatus(profileId, ServerRuntimeState.Stopped, null, null, null, null, null))
            with
            {
                LatestLogLine = line,
            };

        LoadStatus = $"Live log update received for {SelectedProfile.DisplayName}.";
        NotifyComputedState();
        return Task.CompletedTask;
    }

    private Task OnStatusChangedAsync(ServerRuntimeStatus status)
    {
        if (SelectedProfile is null || !string.Equals(SelectedProfile.ProfileId, status.ProfileId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        _runtimeStatus = status;
        LatestRuntimeState = status.State.ToString();
        NotifyComputedState();
        return Task.CompletedTask;
    }

    private Task OnLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot)
    {
        if (SelectedProfile is null || !string.Equals(SelectedProfile.ProfileId, snapshot.ProfileId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        ApplyLiveOperations(snapshot);
        NotifyComputedState();
        return Task.CompletedTask;
    }

    private void ApplyLiveOperations(ProfileLiveOperationsSnapshot? snapshot)
    {
        _liveOperations = snapshot;

        ConnectedPlayers.Clear();
        foreach (var player in snapshot?.ConnectedPlayers ?? [])
        {
            ConnectedPlayers.Add(new ConnectedPlayerRowViewModel(
                player.UserName,
                $"Joined {player.JoinedAtUtc:HH:mm:ss} UTC",
                $"Last seen {player.LastSeenAtUtc:HH:mm:ss} UTC"));
        }

        RecentPlayerSignals.Clear();
        foreach (var signal in snapshot?.RecentPlayerSignals ?? [])
        {
            RecentPlayerSignals.Add($"{signal.TimestampUtc:HH:mm:ss} UTC | {signal.UserName} {signal.Activity.ToLowerInvariant()}");
        }

        RecentOperatorActions.Clear();
        foreach (var action in snapshot?.RecentOperatorActions ?? [])
        {
            RecentOperatorActions.Add($"{action.TimestampUtc:HH:mm:ss} UTC | {action.Kind} | {action.CommandText}");
        }
    }

    private void Reset()
    {
        LogLines.Clear();
        ConnectedPlayers.Clear();
        RecentPlayerSignals.Clear();
        RecentOperatorActions.Clear();
        _runtimeStatus = null;
        _liveOperations = null;
        LatestRuntimeState = "Unknown";
        LoadStatus = "Select a profile to load recent logs.";
        BroadcastMessage = string.Empty;
        RawConsoleCommand = string.Empty;
        NotifyComputedState();
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
        OnPropertyChanged(nameof(HasConnectedPlayers));
        OnPropertyChanged(nameof(HasNoConnectedPlayers));
        OnPropertyChanged(nameof(HasPlayerSignals));
        OnPropertyChanged(nameof(HasNoPlayerSignals));
        OnPropertyChanged(nameof(HasOperatorActions));
        OnPropertyChanged(nameof(HasNoOperatorActions));
        OnPropertyChanged(nameof(LogStreamSummary));
        OnPropertyChanged(nameof(FeedPostureSummary));
        OnPropertyChanged(nameof(FeedHealthHeadline));
        OnPropertyChanged(nameof(IncidentHeadline));
        OnPropertyChanged(nameof(RuntimeGuidance));
        OnPropertyChanged(nameof(LatestLineSummary));
        OnPropertyChanged(nameof(LatestSignalLabel));
        OnPropertyChanged(nameof(OperatorNextStep));
        OnPropertyChanged(nameof(ConsoleStatusSummary));
        OnPropertyChanged(nameof(RuntimeWindowSummary));
        OnPropertyChanged(nameof(PlayerActivityHeadline));
        OnPropertyChanged(nameof(ConsoleHeroTitle));
        OnPropertyChanged(nameof(ConsoleHeroCopy));
        OnPropertyChanged(nameof(ActivePlayerCountSummary));
        OnPropertyChanged(nameof(BufferCountSummary));
        OnPropertyChanged(nameof(SignalCountSummary));
        OnPropertyChanged(nameof(RosterPostureSummary));
        OnPropertyChanged(nameof(PlayerModerationSummary));
        OnPropertyChanged(nameof(PlayerSignalSummary));
        OnPropertyChanged(nameof(PlayerSignalCountSummary));
        OnPropertyChanged(nameof(OperatorActionHeadline));
        OnPropertyChanged(nameof(OperatorActionSummary));
        OnPropertyChanged(nameof(OperatorActionCountSummary));
        OnPropertyChanged(nameof(OperatorCommandPosture));
        OnPropertyChanged(nameof(LiveOpsOperatorSummary));
        OnPropertyChanged(nameof(LiveOpsTriageSummary));
        OnPropertyChanged(nameof(BroadcastGuidance));
        OnPropertyChanged(nameof(RosterSignalSummary));
        OnPropertyChanged(nameof(ConsoleModeSummary));
        OnPropertyChanged(nameof(LiveOpsChecklist));
        OnPropertyChanged(nameof(CanSendCommands));
    }

    private static string QuoteConsoleArgument(string value) =>
        $"\"{value.Replace("\"", "'", StringComparison.Ordinal).Trim()}\"";

    private ProjectZomboidLogPostureSummary CurrentSummary =>
        SelectedProfile is null
            ? EmptySummary
            : ProjectZomboidLogPostureSummaryBuilder.Build(_runtimeStatus, LogLines.ToList());

    private ProjectZomboidLiveOpsConsoleSummary CurrentConsoleSummary =>
        SelectedProfile is null
            ? ProjectZomboidLiveOpsConsoleSummaryBuilder.Empty()
            : ProjectZomboidLiveOpsConsoleSummaryBuilder.Build(
                CurrentSummary,
                LatestRuntimeState,
                CanSendCommands,
                ConnectedPlayers.Count,
                RecentOperatorActions.Count);

    public sealed record ConnectedPlayerRowViewModel(
        string UserName,
        string JoinedSummary,
        string LastSeenSummary);
}
