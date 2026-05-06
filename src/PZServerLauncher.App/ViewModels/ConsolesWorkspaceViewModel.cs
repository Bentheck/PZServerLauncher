using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class ConsolesWorkspaceViewModel : WorkspacePageViewModelBase
{
    private const int ConsoleBufferLimit = 500;
    private static readonly TimeSpan LiveRefreshInterval = TimeSpan.FromSeconds(3);

    private readonly ConsoleWorkspaceStateService _workspaceStateService;
    private bool _hasInitializedSlots;
    private readonly DispatcherTimer _liveRefreshTimer;
    private bool _isLiveRefreshRunning;
    private int _liveRefreshCycle;

    public ConsolesWorkspaceViewModel(
        MainWindowViewModel legacy,
        ILauncherRuntime runtime,
        ConsoleWorkspaceStateService? workspaceStateService = null)
        : base(
            "Consoles",
            "Pin up to four live server consoles, swap them from the roster, and keep the active runtime output one workspace click away.",
            "Consoles are in sync.",
            ["Pinned 4-up board", "Running servers first", "Fast log access", "Compact live ops"])
    {
        Legacy = legacy;
        _workspaceStateService = workspaceStateService ?? new ConsoleWorkspaceStateService();

        if (Legacy.Profiles is INotifyCollectionChanged profiles)
        {
            profiles.CollectionChanged += OnProfilesChanged;
        }

        TrackProfiles(Legacy.Profiles);

        ConsoleSlots =
        [
            new ConsoleTileViewModel(1, legacy, runtime),
            new ConsoleTileViewModel(2, legacy, runtime),
            new ConsoleTileViewModel(3, legacy, runtime),
            new ConsoleTileViewModel(4, legacy, runtime),
        ];

        SelectedSlot = ConsoleSlots[0];
        UpdateSelectedSlotState();

        SelectSlotCommand = new RelayCommand<ConsoleTileViewModel>(SelectSlot);
        AssignProfileCommand = new AsyncRelayCommand<ConsoleProfilePickerItemViewModel>(AssignProfileAsync);
        ClearSelectedSlotCommand = new RelayCommand(ClearSelectedSlot);
        RefreshSelectedSlotCommand = new AsyncRelayCommand(RefreshSelectedSlotAsync);

        _liveRefreshTimer = new DispatcherTimer
        {
            Interval = LiveRefreshInterval,
        };
        _liveRefreshTimer.Tick += OnLiveRefreshTimerTick;

        RestorePersistedState();
        SeedEmptySlots();
        RefreshComputedState();
    }

    public MainWindowViewModel Legacy { get; }

    public ObservableCollection<ConsoleTileViewModel> ConsoleSlots { get; }

    public override string PageSummary => HasProfiles
        ? $"Pin up to four consoles from {Legacy.Profiles.Count} managed server(s) and keep running servers at the front while the board auto-refreshes."
        : "Add or import a server first, then pin its console here for faster live operations.";

    public bool HasProfiles => Legacy.Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public int RunningProfileCount => Legacy.Profiles.Count(profile => IsRunning(profile));

    public int VisibleConsoleCount => ConsoleSlots.Count(slot => slot.HasPinnedProfile);

    public IReadOnlyList<ConsoleProfilePickerItemViewModel> ProfilePickerItems =>
        Legacy.Profiles
            .OrderByDescending(IsRunning)
            .ThenBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(profile =>
            {
                var assignedSlot = ConsoleSlots.FirstOrDefault(slot => slot.Matches(profile.ProfileId));
                return new ConsoleProfilePickerItemViewModel(profile, assignedSlot?.SlotNumber);
            })
            .ToArray();

    public string RunningServersSummary => RunningProfileCount == 0
        ? "No servers are currently running."
        : $"{RunningProfileCount} server{(RunningProfileCount == 1 ? string.Empty : "s")} running right now.";

    public string VisibleConsoleSummary => VisibleConsoleCount == 0
        ? "No console slots are pinned yet."
        : $"{VisibleConsoleCount} of 4 console slot{(VisibleConsoleCount == 1 ? string.Empty : "s")} in use.";

    public string AutoRefreshSummary => $"Live sync runs about every {LiveRefreshInterval.TotalSeconds:0} seconds, with runtime events filling in between polls.";

    public string PickerSummary => HasProfiles
        ? "Choose the target slot, then pin any server from the roster. Running servers stay at the top of the list."
        : "Create or import a server to populate the console roster.";

    public string SelectionSummary => SelectedSlot.HasPinnedProfile
        ? $"{SelectedSlot.SlotLabel} is targeting {SelectedSlot.ProfileDisplayName}."
        : $"{SelectedSlot.SlotLabel} is empty. Pick a server from the roster to pin it.";

    public IRelayCommand<ConsoleTileViewModel> SelectSlotCommand { get; }

    public IAsyncRelayCommand<ConsoleProfilePickerItemViewModel> AssignProfileCommand { get; }

    public IRelayCommand ClearSelectedSlotCommand { get; }

    public IAsyncRelayCommand RefreshSelectedSlotCommand { get; }

    [ObservableProperty]
    private ConsoleTileViewModel selectedSlot = null!;

    public override async Task RefreshPageAsync()
    {
        ResumeLiveRefresh();
        RebindSlots(clearMissingProfiles: true);
        SeedEmptySlots();

        await RefreshPinnedSlotsAsync(includeLogs: true, updateStatusMessage: true);

        RefreshComputedState();
    }

    public void ResumeLiveRefresh()
    {
        if (!_liveRefreshTimer.IsEnabled)
        {
            _liveRefreshTimer.Start();
        }
    }

    public void SuspendLiveRefresh()
    {
        if (_liveRefreshTimer.IsEnabled)
        {
            _liveRefreshTimer.Stop();
        }
    }

    partial void OnSelectedSlotChanged(ConsoleTileViewModel value)
    {
        UpdateSelectedSlotState();
        OnPropertyChanged(nameof(SelectionSummary));
        PersistState();
    }

    private void SelectSlot(ConsoleTileViewModel? slot)
    {
        if (slot is null)
        {
            return;
        }

        SelectedSlot = slot;
    }

    private async Task AssignProfileAsync(ConsoleProfilePickerItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var existingSlot = ConsoleSlots.FirstOrDefault(slot => slot.Matches(item.Profile.ProfileId));
        if (existingSlot is not null)
        {
            SelectedSlot = existingSlot;
            await existingSlot.RefreshAsync();
            RefreshComputedState();
            return;
        }

        SelectedSlot.SetPinnedProfile(item.Profile);
        PersistState();
        RefreshComputedState();
    }

    private void ClearSelectedSlot()
    {
        SelectedSlot.ClearPinnedProfile();
        PersistState();
        SeedEmptySlots();
        RefreshComputedState();
    }

    private async Task RefreshSelectedSlotAsync()
    {
        await SelectedSlot.RefreshAsync();
        RefreshComputedState();
    }

    private async void OnLiveRefreshTimerTick(object? sender, EventArgs e)
    {
        if (_isLiveRefreshRunning || VisibleConsoleCount == 0)
        {
            return;
        }

        _isLiveRefreshRunning = true;

        try
        {
            _liveRefreshCycle++;
            var includeLogs = _liveRefreshCycle % 2 == 0;
            await RefreshPinnedSlotsAsync(includeLogs, updateStatusMessage: false);
        }
        finally
        {
            _isLiveRefreshRunning = false;
        }
    }

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ProfileCardViewModel profile in e.OldItems)
            {
                profile.PropertyChanged -= OnProfilePropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ProfileCardViewModel profile in e.NewItems)
            {
                profile.PropertyChanged += OnProfilePropertyChanged;
            }
        }

        RebindSlots(clearMissingProfiles: false);
        SeedEmptySlots();
        RefreshComputedState();
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(ProfileCardViewModel.RuntimeState)
            or nameof(ProfileCardViewModel.LatestLogLine))
        {
            RefreshComputedState();
        }
    }

    private void TrackProfiles(IEnumerable<ProfileCardViewModel> profiles)
    {
        foreach (var profile in profiles)
        {
            profile.PropertyChanged += OnProfilePropertyChanged;
        }
    }

    private void RebindSlots(bool clearMissingProfiles)
    {
        var lookup = Legacy.Profiles.ToDictionary(profile => profile.ProfileId, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var slot in ConsoleSlots)
        {
            changed |= slot.Rebind(lookup, clearMissingProfiles);
        }

        if (changed)
        {
            PersistState();
        }
    }

    private void SeedEmptySlots()
    {
        if (_hasInitializedSlots || !HasProfiles)
        {
            return;
        }

        var assignedAny = false;
        foreach (var slot in ConsoleSlots.Where(slot => !slot.HasPinnedProfile))
        {
            var candidate = ProfilePickerItems.FirstOrDefault(item => item.AssignedSlotNumber is null);
            if (candidate is null)
            {
                break;
            }

            slot.SetPinnedProfile(candidate.Profile);
            assignedAny = true;
        }

        _hasInitializedSlots = ConsoleSlots.Any(slot => slot.HasPinnedProfile);
        if (assignedAny)
        {
            PersistState();
        }
    }

    private void UpdateSelectedSlotState()
    {
        foreach (var slot in ConsoleSlots)
        {
            slot.IsSelectionTarget = ReferenceEquals(slot, SelectedSlot);
        }
    }

    private async Task RefreshPinnedSlotsAsync(bool includeLogs, bool updateStatusMessage)
    {
        var pinnedSlots = ConsoleSlots.Where(slot => slot.HasPinnedProfile).ToArray();
        if (pinnedSlots.Length == 0)
        {
            return;
        }

        await Task.WhenAll(pinnedSlots.Select(slot => slot.RefreshAsync(includeLogs, updateStatusMessage)));
    }

    private void RefreshComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
        OnPropertyChanged(nameof(RunningProfileCount));
        OnPropertyChanged(nameof(VisibleConsoleCount));
        OnPropertyChanged(nameof(ProfilePickerItems));
        OnPropertyChanged(nameof(RunningServersSummary));
        OnPropertyChanged(nameof(VisibleConsoleSummary));
        OnPropertyChanged(nameof(AutoRefreshSummary));
        OnPropertyChanged(nameof(PickerSummary));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void RestorePersistedState()
    {
        var state = _workspaceStateService.Load();
        if (state is null)
        {
            return;
        }

        foreach (var slot in ConsoleSlots)
        {
            var profileId = state.GetProfileId(slot.SlotNumber);
            if (string.IsNullOrWhiteSpace(profileId))
            {
                continue;
            }

            var profile = Legacy.Profiles.FirstOrDefault(candidate => string.Equals(candidate.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));
            slot.RestorePinnedProfile(profileId, profile);
        }

        var selectedSlot = ConsoleSlots.FirstOrDefault(slot => slot.SlotNumber == state.SelectedSlotNumber);
        if (selectedSlot is not null)
        {
            SelectedSlot = selectedSlot;
        }

        _hasInitializedSlots = ConsoleSlots.Any(slot => slot.HasPinnedProfile);
    }

    private void PersistState()
    {
        _workspaceStateService.Save(new ConsoleWorkspaceState(
            SelectedSlot.SlotNumber,
            ConsoleSlots.Select(slot => new ConsoleSlotState(slot.SlotNumber, slot.AssignedProfileId)).ToArray()));
    }

    private static bool IsRunning(ProfileCardViewModel profile) =>
        string.Equals(profile.RuntimeState, ServerRuntimeState.Running.ToString(), StringComparison.OrdinalIgnoreCase);

    public sealed record ConsoleProfilePickerItemViewModel(ProfileCardViewModel Profile, int? AssignedSlotNumber)
    {
        public string DisplayName => Profile.DisplayName;

        public string Branch => Profile.Branch;

        public string RuntimeState => Profile.RuntimeState;

        public string LatestLogLine => Profile.PinnedLatestSignal;

        public bool IsAssigned => AssignedSlotNumber is not null;

        public string AssignmentLabel => AssignedSlotNumber is int slotNumber
            ? $"Visible in slot {slotNumber}"
            : "Pin to selected slot";
    }

    public sealed partial class ConsoleTileViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _legacy;
        private readonly ILauncherRuntime _runtime;
        private ServerRuntimeStatus? _runtimeStatus;
        private ProfileLiveOperationsSnapshot? _liveOperations;
        private ProfileCardViewModel? _profile;
        private string? _assignedProfileId;

        public ConsoleTileViewModel(
            int slotNumber,
            MainWindowViewModel legacy,
            ILauncherRuntime runtime)
        {
            SlotNumber = slotNumber;
            SlotLabel = $"Slot {slotNumber}";
            _legacy = legacy;
            _runtime = runtime;
            _runtime.LogLineReceived += OnLogLineReceivedAsync;
            _runtime.StatusChanged += OnStatusChangedAsync;
            _runtime.LiveOperationsChanged += OnLiveOperationsChangedAsync;

            ReloadCommand = new AsyncRelayCommand(() => RefreshAsync(includeLogs: true, updateStatusMessage: true));
            PrimaryRuntimeCommand = new AsyncRelayCommand(ExecutePrimaryRuntimeActionAsync);
            RestartRuntimeCommand = new AsyncRelayCommand(ExecuteRestartAsync);
            RequestPlayersCommand = new AsyncRelayCommand(() => SendQuickCommandAsync("players", "Requested the live player list."));
            SaveWorldCommand = new AsyncRelayCommand(() => SendQuickCommandAsync("save", "Requested an in-place world save."));
            SendRawCommand = new AsyncRelayCommand(SendRawCommandAsync);

            LoadStatus = "Choose a server to load this console slot.";
        }

        public int SlotNumber { get; }

        public string SlotLabel { get; }

        public ObservableCollection<string> LogLines { get; } = [];

        public bool HasPinnedProfile => !string.IsNullOrWhiteSpace(_assignedProfileId);

        public bool HasProfile => _profile is not null;

        public string? AssignedProfileId => _assignedProfileId;

        public string ProfileDisplayName => _profile?.DisplayName ?? (HasPinnedProfile ? "Refreshing pinned server..." : "No server pinned");

        public string Branch => _profile?.Branch ?? "No branch";

        public string RuntimeState => LatestRuntimeState;

        public string TileBorderBrush => IsSelectionTarget ? "#D27A3E" : "#3F565F";

        public string SlotSummary => HasPinnedProfile
            ? $"{ProfileDisplayName} | {RuntimeState}"
            : "Choose a server from the roster.";

        public string StatusSummary => HasPinnedProfile
            ? $"{RuntimeState} | {LogLines.Count} line(s) buffered | {ActivePlayerCountSummary}"
            : "Waiting for a pinned server.";

        public string ActivePlayerCountSummary => _liveOperations is null
            ? "No live roster sampled"
            : _liveOperations.ConnectedPlayers.Count == 0
                ? "No players inferred online"
                : $"{_liveOperations.ConnectedPlayers.Count} player(s) online";

        public string ActivitySummary => _liveOperations is null
            ? _runtimeStatus?.WorkshopDownloadProgress?.DetailLabel ?? "No live operator data loaded yet."
            : _runtimeStatus?.WorkshopDownloadProgress is { } workshopProgress
                ? workshopProgress.DetailLabel
            : _liveOperations.RecentOperatorActions.Count > 0
                ? _liveOperations.RecentOperatorActions[0].Summary
                : _liveOperations.RecentPlayerSignals.Count > 0
                    ? $"{_liveOperations.RecentPlayerSignals[0].UserName} {_liveOperations.RecentPlayerSignals[0].Activity.ToLowerInvariant()} at {_liveOperations.RecentPlayerSignals[0].TimestampUtc:HH:mm:ss} UTC."
                    : "No recent player or operator signals in this slot.";

        public bool IsRunning => string.Equals(LatestRuntimeState, ServerRuntimeState.Running.ToString(), StringComparison.OrdinalIgnoreCase);

        public bool ShowStartAction => HasPinnedProfile && !IsRunning;

        public bool ShowStopAction => HasPinnedProfile && IsRunning;

        public bool CanSendCommands => HasPinnedProfile && IsRunning;

        public string LogText => HasPinnedProfile
            ? LogLines.Count == 0
                ? "No buffered logs yet. Start or reload the server to populate this console."
                : string.Join(Environment.NewLine, LogLines)
            : "Pin a server from the roster to open a live console in this slot.";

        public int ConsoleCaretIndex => FollowTail ? LogText.Length : 0;

        public IAsyncRelayCommand ReloadCommand { get; }

        public IAsyncRelayCommand PrimaryRuntimeCommand { get; }

        public IAsyncRelayCommand RestartRuntimeCommand { get; }

        public IAsyncRelayCommand RequestPlayersCommand { get; }

        public IAsyncRelayCommand SaveWorldCommand { get; }

        public IAsyncRelayCommand SendRawCommand { get; }

        [ObservableProperty]
        private bool isSelectionTarget;

        [ObservableProperty]
        private bool followTail = true;

        [ObservableProperty]
        private string loadStatus;

        [ObservableProperty]
        private string latestRuntimeState = "Unknown";

        [ObservableProperty]
        private string rawConsoleCommand = string.Empty;

        partial void OnIsSelectionTargetChanged(bool value)
        {
            OnPropertyChanged(nameof(TileBorderBrush));
        }

        partial void OnFollowTailChanged(bool value)
        {
            OnPropertyChanged(nameof(ConsoleCaretIndex));
        }

        public void SetPinnedProfile(ProfileCardViewModel? profile)
        {
            _assignedProfileId = profile?.ProfileId;
            _profile = profile;

            if (profile is null)
            {
                Reset();
                return;
            }

            _ = RefreshAsync(includeLogs: true, updateStatusMessage: true);
            NotifyComputedState();
        }

        public void RestorePinnedProfile(string profileId, ProfileCardViewModel? profile)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                ClearPinnedProfile();
                return;
            }

            _assignedProfileId = profileId;
            _profile = profile;

            if (profile is null)
            {
                LogLines.Clear();
                _runtimeStatus = null;
                _liveOperations = null;
                LatestRuntimeState = "Unknown";
                LoadStatus = "Pinned server will reconnect when the roster loads.";
                NotifyComputedState();
                return;
            }

            _ = RefreshAsync(includeLogs: true, updateStatusMessage: true);
            NotifyComputedState();
        }

        public void ClearPinnedProfile()
        {
            _assignedProfileId = null;
            _profile = null;
            Reset();
        }

        public bool Rebind(IReadOnlyDictionary<string, ProfileCardViewModel> profiles, bool clearMissingProfiles)
        {
            if (string.IsNullOrWhiteSpace(_assignedProfileId))
            {
                return false;
            }

            if (profiles.TryGetValue(_assignedProfileId, out var profile))
            {
                if (!ReferenceEquals(_profile, profile))
                {
                    _profile = profile;
                    _ = RefreshAsync(includeLogs: true, updateStatusMessage: true);
                    NotifyComputedState();
                    return true;
                }

                NotifyComputedState();
                return false;
            }

            if (clearMissingProfiles)
            {
                ClearPinnedProfile();
                return true;
            }

            if (_profile is not null)
            {
                _profile = null;
                LogLines.Clear();
                _runtimeStatus = null;
                _liveOperations = null;
                LatestRuntimeState = "Unknown";
                LoadStatus = "Pinned server will reconnect when the roster loads.";
                NotifyComputedState();
                return true;
            }

            return false;
        }

        public bool Matches(string profileId) =>
            string.Equals(_assignedProfileId, profileId, StringComparison.Ordinal);

        public async Task RefreshAsync(bool includeLogs = true, bool updateStatusMessage = true)
        {
            if (_profile is null)
            {
                if (!HasPinnedProfile)
                {
                    Reset();
                }

                return;
            }

            try
            {
                await LoadAsync(_profile, includeLogs, updateStatusMessage);
            }
            catch (Exception ex)
            {
                LoadStatus = ex.Message;
                NotifyComputedState();
            }
        }

        private async Task ExecutePrimaryRuntimeActionAsync()
        {
            if (_profile is null)
            {
                return;
            }

            if (IsRunning)
            {
                await _legacy.StopCommand.ExecuteAsync(_profile);
                LoadStatus = $"Stop requested for {_profile.DisplayName}.";
            }
            else
            {
                await _legacy.StartCommand.ExecuteAsync(_profile);
                LoadStatus = $"Start requested for {_profile.DisplayName}.";
            }

            await RefreshAsync(includeLogs: true, updateStatusMessage: true);
        }

        private async Task ExecuteRestartAsync()
        {
            if (_profile is null)
            {
                return;
            }

            await _legacy.RestartCommand.ExecuteAsync(_profile);
            LoadStatus = $"Restart requested for {_profile.DisplayName}.";
            await RefreshAsync(includeLogs: true, updateStatusMessage: true);
        }

        private async Task SendRawCommandAsync()
        {
            if (string.IsNullOrWhiteSpace(RawConsoleCommand))
            {
                return;
            }

            await SendQuickCommandAsync(RawConsoleCommand, $"Queued console command for {ProfileDisplayName}.");
            RawConsoleCommand = string.Empty;
        }

        private async Task SendQuickCommandAsync(string command, string statusMessage)
        {
            if (_profile is null || string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            try
            {
                var response = await _runtime.SendConsoleCommandAsync(_profile.ProfileId, command);
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

        private async Task LoadAsync(ProfileCardViewModel profile, bool includeLogs, bool updateStatusMessage)
        {
            if (updateStatusMessage)
            {
                LoadStatus = $"Loading {profile.DisplayName}...";
            }

            var statusTask = _runtime.GetStatusAsync(profile.ProfileId);
            var operationsTask = _runtime.GetLiveOperationsAsync(profile.ProfileId);
            Task<List<string>?>? logsTask = includeLogs
                ? _runtime.GetRecentLogsAsync(profile.ProfileId)
                : null;

            if (logsTask is null)
            {
                await Task.WhenAll(statusTask, operationsTask);
            }
            else
            {
                await Task.WhenAll(statusTask, logsTask, operationsTask);
            }

            _runtimeStatus = statusTask.Result
                ?? new ServerRuntimeStatus(profile.ProfileId, ServerRuntimeState.Stopped, null, null, null, null, profile.LatestLogLine);
            LatestRuntimeState = _runtimeStatus.State.ToString();

            if (logsTask is not null)
            {
                ApplyLogBuffer(logsTask.Result ?? []);
            }

            ApplyLiveOperations(operationsTask.Result);

            if (updateStatusMessage)
            {
                LoadStatus = LogLines.Count == 0
                    ? $"No buffered lines yet for {profile.DisplayName}."
                    : $"Loaded {LogLines.Count} recent line(s) for {profile.DisplayName}.";
            }

            NotifyComputedState();
        }

        private Task OnLogLineReceivedAsync(string profileId, string line)
        {
            if (!Matches(profileId))
            {
                return Task.CompletedTask;
            }

            Dispatcher.UIThread.Post(() =>
            {
                LogLines.Add(line);
                while (LogLines.Count > ConsoleBufferLimit)
                {
                    LogLines.RemoveAt(0);
                }

                _runtimeStatus = (_runtimeStatus ?? new ServerRuntimeStatus(profileId, ServerRuntimeState.Stopped, null, null, null, null, null))
                    with
                    {
                        LatestLogLine = line,
                    };

                if (_profile is not null)
                {
                    _profile.LatestLogLine = line;
                }

                LoadStatus = $"Live log update received for {ProfileDisplayName}.";
                NotifyComputedState();
            });

            return Task.CompletedTask;
        }

        private Task OnStatusChangedAsync(ServerRuntimeStatus status)
        {
            if (!Matches(status.ProfileId))
            {
                return Task.CompletedTask;
            }

            Dispatcher.UIThread.Post(() =>
            {
                _runtimeStatus = status;
                if (_profile is not null)
                {
                    _profile.WorkshopDownloadProgress = status.WorkshopDownloadProgress;
                    if (!string.IsNullOrWhiteSpace(status.LatestLogLine))
                    {
                        _profile.LatestLogLine = status.LatestLogLine;
                    }
                }

                LatestRuntimeState = status.State.ToString();
                NotifyComputedState();
            });

            return Task.CompletedTask;
        }

        private Task OnLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot)
        {
            if (!Matches(snapshot.ProfileId))
            {
                return Task.CompletedTask;
            }

            Dispatcher.UIThread.Post(() =>
            {
                ApplyLiveOperations(snapshot);
                NotifyComputedState();
            });

            return Task.CompletedTask;
        }

        private void ApplyLiveOperations(ProfileLiveOperationsSnapshot? snapshot)
        {
            _liveOperations = snapshot;
        }

        private void ApplyLogBuffer(IEnumerable<string> lines)
        {
            LogLines.Clear();
            foreach (var line in lines)
            {
                LogLines.Add(line);
            }

            while (LogLines.Count > ConsoleBufferLimit)
            {
                LogLines.RemoveAt(0);
            }
        }

        private void Reset()
        {
            LogLines.Clear();
            _runtimeStatus = null;
            _liveOperations = null;
            LatestRuntimeState = "Unknown";
            RawConsoleCommand = string.Empty;
            LoadStatus = "Choose a server to load this console slot.";
            NotifyComputedState();
        }

        private void NotifyComputedState()
        {
            OnPropertyChanged(nameof(HasPinnedProfile));
            OnPropertyChanged(nameof(HasProfile));
            OnPropertyChanged(nameof(ProfileDisplayName));
            OnPropertyChanged(nameof(Branch));
            OnPropertyChanged(nameof(RuntimeState));
            OnPropertyChanged(nameof(SlotSummary));
            OnPropertyChanged(nameof(StatusSummary));
            OnPropertyChanged(nameof(ActivePlayerCountSummary));
            OnPropertyChanged(nameof(ActivitySummary));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(ShowStartAction));
            OnPropertyChanged(nameof(ShowStopAction));
            OnPropertyChanged(nameof(CanSendCommands));
            OnPropertyChanged(nameof(LogText));
            OnPropertyChanged(nameof(ConsoleCaretIndex));
        }
    }
}
