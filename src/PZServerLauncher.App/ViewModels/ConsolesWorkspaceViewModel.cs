using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class ConsolesWorkspaceViewModel : WorkspacePageViewModelBase
{
    private bool _hasInitializedSlots;

    public ConsolesWorkspaceViewModel(
        MainWindowViewModel legacy,
        LocalHostApiClient hostApiClient,
        RuntimeEventStream runtimeEventStream)
        : base(
            "Consoles",
            "Pin up to four live server consoles, swap them from the roster, and keep the active runtime output one workspace click away.",
            "Consoles are in sync.",
            ["Pinned 4-up board", "Running servers first", "Fast log access", "Compact live ops"])
    {
        Legacy = legacy;

        if (Legacy.Profiles is INotifyCollectionChanged profiles)
        {
            profiles.CollectionChanged += OnProfilesChanged;
        }

        TrackProfiles(Legacy.Profiles);

        ConsoleSlots =
        [
            new ConsoleTileViewModel(1, legacy, hostApiClient, runtimeEventStream),
            new ConsoleTileViewModel(2, legacy, hostApiClient, runtimeEventStream),
            new ConsoleTileViewModel(3, legacy, hostApiClient, runtimeEventStream),
            new ConsoleTileViewModel(4, legacy, hostApiClient, runtimeEventStream),
        ];

        SelectedSlot = ConsoleSlots[0];
        UpdateSelectedSlotState();

        SelectSlotCommand = new RelayCommand<ConsoleTileViewModel>(SelectSlot);
        AssignProfileCommand = new AsyncRelayCommand<ConsoleProfilePickerItemViewModel>(AssignProfileAsync);
        ClearSelectedSlotCommand = new RelayCommand(ClearSelectedSlot);
        RefreshSelectedSlotCommand = new AsyncRelayCommand(RefreshSelectedSlotAsync);

        SeedEmptySlots();
        RefreshComputedState();
    }

    public MainWindowViewModel Legacy { get; }

    public ObservableCollection<ConsoleTileViewModel> ConsoleSlots { get; }

    public override string PageSummary => HasProfiles
        ? $"Pin up to four consoles from {Legacy.Profiles.Count} managed server(s) and keep running servers at the front of the picker."
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
        RebindSlots(clearMissingProfiles: true);
        SeedEmptySlots();

        foreach (var slot in ConsoleSlots.Where(slot => slot.HasPinnedProfile))
        {
            await slot.RefreshAsync();
        }

        RefreshComputedState();
    }

    partial void OnSelectedSlotChanged(ConsoleTileViewModel value)
    {
        UpdateSelectedSlotState();
        OnPropertyChanged(nameof(SelectionSummary));
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
        RefreshComputedState();
    }

    private void ClearSelectedSlot()
    {
        SelectedSlot.ClearPinnedProfile();
        SeedEmptySlots();
        RefreshComputedState();
    }

    private async Task RefreshSelectedSlotAsync()
    {
        await SelectedSlot.RefreshAsync();
        RefreshComputedState();
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
        foreach (var slot in ConsoleSlots)
        {
            slot.Rebind(lookup, clearMissingProfiles);
        }
    }

    private void SeedEmptySlots()
    {
        if (_hasInitializedSlots || !HasProfiles)
        {
            return;
        }

        foreach (var slot in ConsoleSlots.Where(slot => !slot.HasPinnedProfile))
        {
            var candidate = ProfilePickerItems.FirstOrDefault(item => item.AssignedSlotNumber is null);
            if (candidate is null)
            {
                break;
            }

            slot.SetPinnedProfile(candidate.Profile);
        }

        _hasInitializedSlots = ConsoleSlots.Any(slot => slot.HasPinnedProfile);
    }

    private void UpdateSelectedSlotState()
    {
        foreach (var slot in ConsoleSlots)
        {
            slot.IsSelectionTarget = ReferenceEquals(slot, SelectedSlot);
        }
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
        OnPropertyChanged(nameof(PickerSummary));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private static bool IsRunning(ProfileCardViewModel profile) =>
        string.Equals(profile.RuntimeState, ServerRuntimeState.Running.ToString(), StringComparison.OrdinalIgnoreCase);

    public sealed record ConsoleProfilePickerItemViewModel(ProfileCardViewModel Profile, int? AssignedSlotNumber)
    {
        public string DisplayName => Profile.DisplayName;

        public string Branch => Profile.Branch;

        public string RuntimeState => Profile.RuntimeState;

        public string LatestLogLine => Profile.LatestLogLine;

        public bool IsAssigned => AssignedSlotNumber is not null;

        public string AssignmentLabel => AssignedSlotNumber is int slotNumber
            ? $"Visible in slot {slotNumber}"
            : "Pin to selected slot";
    }

    public sealed partial class ConsoleTileViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _legacy;
        private readonly LocalHostApiClient _hostApiClient;
        private ServerRuntimeStatus? _runtimeStatus;
        private ProfileLiveOperationsSnapshot? _liveOperations;
        private ProfileCardViewModel? _profile;
        private string? _assignedProfileId;

        public ConsoleTileViewModel(
            int slotNumber,
            MainWindowViewModel legacy,
            LocalHostApiClient hostApiClient,
            RuntimeEventStream runtimeEventStream)
        {
            SlotNumber = slotNumber;
            SlotLabel = $"Slot {slotNumber}";
            _legacy = legacy;
            _hostApiClient = hostApiClient;
            runtimeEventStream.LogLineReceived += OnLogLineReceivedAsync;
            runtimeEventStream.StatusChanged += OnStatusChangedAsync;
            runtimeEventStream.LiveOperationsChanged += OnLiveOperationsChangedAsync;

            ReloadCommand = new AsyncRelayCommand(RefreshAsync);
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
            ? "No live operator data loaded yet."
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

            _ = LoadAsync(profile);
            NotifyComputedState();
        }

        public void ClearPinnedProfile()
        {
            _assignedProfileId = null;
            _profile = null;
            Reset();
        }

        public void Rebind(IReadOnlyDictionary<string, ProfileCardViewModel> profiles, bool clearMissingProfiles)
        {
            if (string.IsNullOrWhiteSpace(_assignedProfileId))
            {
                return;
            }

            if (profiles.TryGetValue(_assignedProfileId, out var profile))
            {
                if (!ReferenceEquals(_profile, profile))
                {
                    _profile = profile;
                    _ = LoadAsync(profile);
                }

                NotifyComputedState();
                return;
            }

            if (clearMissingProfiles)
            {
                ClearPinnedProfile();
            }
        }

        public bool Matches(string profileId) =>
            string.Equals(_assignedProfileId, profileId, StringComparison.Ordinal);

        public async Task RefreshAsync()
        {
            if (_profile is null)
            {
                if (!HasPinnedProfile)
                {
                    Reset();
                }

                return;
            }

            await LoadAsync(_profile);
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

            await RefreshAsync();
        }

        private async Task ExecuteRestartAsync()
        {
            if (_profile is null)
            {
                return;
            }

            await _legacy.RestartCommand.ExecuteAsync(_profile);
            LoadStatus = $"Restart requested for {_profile.DisplayName}.";
            await RefreshAsync();
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
                var response = await _hostApiClient.SendConsoleCommandAsync(_profile.ProfileId, command);
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

        private async Task LoadAsync(ProfileCardViewModel profile)
        {
            LoadStatus = $"Loading {profile.DisplayName}...";
            var statusTask = _hostApiClient.GetStatusAsync(profile.ProfileId);
            var logsTask = _hostApiClient.GetRecentLogsAsync(profile.ProfileId);
            var operationsTask = _hostApiClient.GetLiveOperationsAsync(profile.ProfileId);
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
                ? $"No buffered lines yet for {profile.DisplayName}."
                : $"Loaded {LogLines.Count} recent line(s) for {profile.DisplayName}.";
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
                while (LogLines.Count > 250)
                {
                    LogLines.RemoveAt(0);
                }

                _runtimeStatus = (_runtimeStatus ?? new ServerRuntimeStatus(profileId, ServerRuntimeState.Stopped, null, null, null, null, null))
                    with
                    {
                        LatestLogLine = line,
                    };

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
