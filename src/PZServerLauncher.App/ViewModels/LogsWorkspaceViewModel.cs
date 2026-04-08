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
        : $"Recent runtime output for {SelectedProfile.DisplayName}.";

    public ObservableCollection<string> LogLines { get; } = [];

    public bool HasLogs => LogLines.Count > 0;

    public bool HasNoLogs => LogLines.Count == 0;

    public IAsyncRelayCommand ReloadCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load recent logs.";

    [ObservableProperty]
    private string latestRuntimeState = "Unknown";

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
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

        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
        LoadStatus = lines.Count == 0
            ? "No recent log lines are buffered yet. Start the server or wait for new runtime output."
            : $"Loaded {lines.Count} recent log line(s) for {profile.DisplayName}.";
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

        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
        LoadStatus = $"Live log update received for {SelectedProfile.DisplayName}.";
        return Task.CompletedTask;
    }

    private Task OnStatusChangedAsync(ServerRuntimeStatus status)
    {
        if (SelectedProfile is null || !string.Equals(SelectedProfile.ProfileId, status.ProfileId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        LatestRuntimeState = status.State.ToString();
        return Task.CompletedTask;
    }

    private void Reset()
    {
        LogLines.Clear();
        LatestRuntimeState = "Unknown";
        LoadStatus = "Select a profile to load recent logs.";
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
    }
}
