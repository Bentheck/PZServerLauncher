using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.App.ViewModels;

public partial class LogsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private static readonly ProjectZomboidLogPostureSummary EmptySummary = new(
        "No buffered lines are available yet. Select a profile to inspect runtime output.",
        "Latest signal: no runtime output captured yet.",
        "No runtime signals are buffered yet.",
        "Pick a profile, then start or reload to watch runtime output.",
        "Runtime window: no status is available yet.",
        0,
        0,
        0,
        0,
        false,
        false,
        false);

    private readonly LocalHostApiClient _hostApiClient;
    private readonly RuntimeEventStream _runtimeEventStream;
    private ServerRuntimeStatus? _runtimeStatus;

    public LogsWorkspaceViewModel(
        MainWindowViewModel legacy,
        LocalHostApiClient hostApiClient,
        RuntimeEventStream runtimeEventStream)
        : base(
            ProfileWorkspacePageIds.Logs,
            "Logs",
            "Recent runtime output and the latest live line for the selected profile.",
            "Logs are in sync.",
            legacy,
            ["Recent log buffer", "Live line feed", "Runtime state"])
    {
        _hostApiClient = hostApiClient;
        _runtimeEventStream = runtimeEventStream;
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        _runtimeEventStream.LogLineReceived += OnLogLineReceivedAsync;
        _runtimeEventStream.StatusChanged += OnStatusChangedAsync;
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to view runtime output."
        : $"Recent runtime output, live status, and operator guidance for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string ConsoleHeroTitle => SelectedProfile is null
        ? "Live Console"
        : $"{SelectedProfile.DisplayName} Live Console";

    public string ConsoleHeroCopy => SelectedProfile is null
        ? "Select a profile to inspect buffered runtime output and the live feed posture."
        : $"Buffered runtime output, the latest log line, and live operator guidance for {SelectedProfile.DisplayName}.";

    public ObservableCollection<string> LogLines { get; } = [];

    public bool HasLogs => LogLines.Count > 0;

    public bool HasNoLogs => LogLines.Count == 0;

    public string LogStreamSummary => SelectedProfile is null
        ? "Choose a profile to inspect server output."
        : CurrentSummary.BufferSummary;

    public string FeedPostureSummary => SelectedProfile is null
        ? "No live feed is available yet."
        : CurrentSummary.SignalPostureSummary;

    public string RuntimeGuidance => SelectedProfile is null
        ? "No runtime guidance available."
        : CurrentSummary.OperatorFocusSummary;

    public string LatestLineSummary => CurrentSummary.LatestSignalSummary;

    public string OperatorNextStep => SelectedProfile is null
        ? "Pick a profile, then start or reload to watch runtime output."
        : CurrentSummary.OperatorFocusSummary;

    public string ConsoleStatusSummary => SelectedProfile is null
        ? "No console context loaded."
        : $"{LatestRuntimeState} | {CurrentSummary.BufferedLineCount} buffered line(s)";

    public string RuntimeWindowSummary => SelectedProfile is null
        ? "Runtime window: no status is available yet."
        : CurrentSummary.RuntimeWindowSummary;

    public IAsyncRelayCommand ReloadCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load recent logs.";

    [ObservableProperty]
    private string latestRuntimeState = "Unknown";

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        NotifyComputedState();
        _ = LoadAsync(profile);
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

        LoadStatus = $"Loading recent logs for {profile.DisplayName}...";
        _runtimeStatus = await _hostApiClient.GetStatusAsync(profile.ProfileId)
            ?? new ServerRuntimeStatus(profile.ProfileId, ServerRuntimeState.Stopped, null, null, null, null, profile.LatestLogLine);
        LatestRuntimeState = _runtimeStatus.State.ToString();
        var lines = await _hostApiClient.GetRecentLogsAsync(profile.ProfileId) ?? [];

        LogLines.Clear();
        foreach (var line in lines)
        {
            LogLines.Add(line);
        }

        LoadStatus = lines.Count == 0
            ? "No recent log lines are buffered yet. Start the server or wait for new runtime output."
            : $"Loaded {lines.Count} recent log line(s) for {profile.DisplayName}.";
        NotifyComputedState();
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

    private void Reset()
    {
        LogLines.Clear();
        _runtimeStatus = null;
        LatestRuntimeState = "Unknown";
        LoadStatus = "Select a profile to load recent logs.";
        NotifyComputedState();
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
        OnPropertyChanged(nameof(LogStreamSummary));
        OnPropertyChanged(nameof(FeedPostureSummary));
        OnPropertyChanged(nameof(RuntimeGuidance));
        OnPropertyChanged(nameof(LatestLineSummary));
        OnPropertyChanged(nameof(OperatorNextStep));
        OnPropertyChanged(nameof(ConsoleStatusSummary));
        OnPropertyChanged(nameof(RuntimeWindowSummary));
        OnPropertyChanged(nameof(ConsoleHeroTitle));
        OnPropertyChanged(nameof(ConsoleHeroCopy));
    }

    private ProjectZomboidLogPostureSummary CurrentSummary =>
        SelectedProfile is null
            ? EmptySummary
            : ProjectZomboidLogPostureSummaryBuilder.Build(_runtimeStatus, LogLines.ToList());
}
