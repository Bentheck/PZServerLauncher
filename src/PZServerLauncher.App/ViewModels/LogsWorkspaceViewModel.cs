using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class LogsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private readonly RuntimeEventStream _runtimeEventStream;

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

    public ObservableCollection<string> LogLines { get; } = [];

    public bool HasLogs => LogLines.Count > 0;

    public bool HasNoLogs => LogLines.Count == 0;

    public string LogStreamSummary => SelectedProfile is null
        ? "Choose a profile to inspect server output."
        : LogLines.Count == 0
            ? "No buffered output is available yet. Start the server or wait for the next runtime event."
            : $"Showing {LogLines.Count} buffered line(s) for the selected profile.";

    public string RuntimeGuidance => SelectedProfile is null
        ? "No runtime guidance available."
        : string.Equals(LatestRuntimeState, nameof(ServerProcessState.Running), StringComparison.OrdinalIgnoreCase)
            ? "The server is live. Keep this page open during testing, config reloads, and mod validation to watch the latest output."
            : "The server is not currently running. Use Overview or Install & Update to start it, then return here for live output.";

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
        LatestRuntimeState = profile.RuntimeState;
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

        LatestRuntimeState = status.State.ToString();
        NotifyComputedState();
        return Task.CompletedTask;
    }

    private void Reset()
    {
        LogLines.Clear();
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
        OnPropertyChanged(nameof(RuntimeGuidance));
    }
}
