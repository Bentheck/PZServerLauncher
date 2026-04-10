using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class ProfilesWorkspaceViewModel : ViewModelBase, IWorkspacePageHeader, IWorkspaceDirtyState, IWorkspaceRefreshable
{
    private readonly IReadOnlyDictionary<string, ViewModelBase> _sections;
    private string? _selectedProfileId;

    public ProfilesWorkspaceViewModel(
        MainWindowViewModel legacy,
        LocalHostApiClient hostApiClient,
        RuntimeEventStream runtimeEventStream,
        FolderPickerService folderPickerService)
    {
        Legacy = legacy;
        if (Legacy.Profiles is INotifyCollectionChanged profiles)
        {
            profiles.CollectionChanged += OnProfilesChanged;
        }

        if (Legacy.ImportCandidates is INotifyCollectionChanged importCandidates)
        {
            importCandidates.CollectionChanged += OnImportCandidatesChanged;
        }

        Overview = new OverviewWorkspaceViewModel(legacy, hostApiClient, runtimeEventStream);
        InstallAndUpdate = new InstallUpdateWorkspaceViewModel(legacy, hostApiClient, folderPickerService);
        General = new GeneralWorkspaceViewModel(legacy, hostApiClient);
        Sandbox = new SandboxWorkspaceViewModel(legacy, hostApiClient);
        ModsAndMaps = new ModsAndMapsWorkspaceViewModel(legacy, hostApiClient);
        NetworkAndAdmin = new NetworkAndAdminWorkspaceViewModel(legacy, hostApiClient);
        Backups = new BackupsWorkspaceViewModel(legacy, hostApiClient);
        Logs = new LogsWorkspaceViewModel(legacy, hostApiClient, runtimeEventStream);
        AdvancedFiles = new AdvancedFilesWorkspaceViewModel(legacy, hostApiClient);

        _sections = new Dictionary<string, ViewModelBase>(StringComparer.Ordinal)
        {
            [ProfileWorkspacePageIds.Overview] = Overview,
            [ProfileWorkspacePageIds.InstallAndUpdate] = InstallAndUpdate,
            [ProfileWorkspacePageIds.General] = General,
            [ProfileWorkspacePageIds.Sandbox] = Sandbox,
            [ProfileWorkspacePageIds.ModsAndMaps] = ModsAndMaps,
            [ProfileWorkspacePageIds.NetworkAndAdmin] = NetworkAndAdmin,
            [ProfileWorkspacePageIds.Backups] = Backups,
            [ProfileWorkspacePageIds.Logs] = Logs,
            [ProfileWorkspacePageIds.AdvancedFiles] = AdvancedFiles,
        };

        SectionItems =
        [
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.Overview, "Overview", "Runtime state, latest log, and quick actions."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.InstallAndUpdate, "Install & Update", "Install state, branch, and lifecycle actions."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.General, "General", "Structured server name, ports, startup, and memory."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.Sandbox, "Sandbox", "Branch-specific gameplay and world settings."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.ModsAndMaps, "Mods & Maps", "Workshop, mods, map ordering, and presets."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.NetworkAndAdmin, "Network & Admin", "Network-facing server options and admin controls."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.Backups, "Backups", "Manual backups, restore, and retention."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.Logs, "Logs", "Live runtime output and recent history."),
            new WorkspaceNavigationItemViewModel(ProfileWorkspacePageIds.AdvancedFiles, "Advanced Files", "Raw config editors for unsupported cases."),
        ];

        CurrentSection = Overview;
        UpdateSectionSelection(ProfileWorkspacePageIds.Overview);

        SelectSectionCommand = new AsyncRelayCommand<WorkspaceNavigationItemViewModel>(SelectSectionAsync);
        SaveCurrentDraftCommand = new AsyncRelayCommand(SaveDraftAsync);
        DiscardCurrentDraftCommand = new AsyncRelayCommand(DiscardDraftAsync);
        ConfirmSectionNavigationSaveCommand = new AsyncRelayCommand(ConfirmSectionNavigationSaveAsync);
        ConfirmSectionNavigationDiscardCommand = new AsyncRelayCommand(ConfirmSectionNavigationDiscardAsync);
        CancelSectionNavigationCommand = new RelayCommand(CancelSectionNavigation);
    }

    public MainWindowViewModel Legacy { get; }

    public string PageTitle => "Profiles";

    public string PageSummary => "Choose a profile, then move through the full per-profile workspace. Every per-profile surface now lives here.";

    public string WorkspaceHeadline => SelectedProfile is null
        ? "No profile selected"
        : $"{SelectedProfile.DisplayName} is ready for operator work.";

    public string WorkspaceGuidance => SelectedProfile is null
        ? "Pick a profile on the left to reveal the workspace sections, install state, and runtime context."
        : $"Use the section rail to jump between Overview, install/update, structured settings, backups, logs, and raw files for {SelectedProfile.DisplayName}.";

    public string ProfileCountSummary => Legacy.Profiles.Count == 0
        ? "No profiles"
        : $"{Legacy.Profiles.Count} profile(s) available";

    public bool HasProfiles => Legacy.Profiles.Count > 0;

    public bool HasNoProfiles => !HasProfiles;

    public bool HasImportCandidates => Legacy.ImportCandidates.Count > 0;

    public int ImportCandidateCount => Legacy.ImportCandidates.Count;

    public string FirstRunHeadline => HasImportCandidates
        ? $"{ImportCandidateCount} local server candidate(s) are ready for intake."
        : "Create or import the first managed server.";

    public string FirstRunActionPlan => HasImportCandidates
        ? "Import one candidate first, then use the section rail to verify install, recovery, and configuration posture."
        : "Create a starter profile or scan the machine for an existing Zomboid install, then continue here once the roster has its first server.";

    public string FirstRunStepOne => HasImportCandidates
        ? "Step 1: Choose the local server you want to bring under management."
        : "Step 1: Create a starter profile or run import discovery.";

    public string FirstRunStepTwo => "Step 2: Open Install & Update to establish the server footprint and branch isolation.";

    public string FirstRunStepThree => "Step 3: Use the section rail to finish settings, backups, and the first launch.";

    public IReadOnlyList<WorkspaceNavigationItemViewModel> SectionItems { get; }

    public IReadOnlyList<ProfileCardViewModel> Profiles => Legacy.Profiles;

    public OverviewWorkspaceViewModel Overview { get; }

    public InstallUpdateWorkspaceViewModel InstallAndUpdate { get; }

    public GeneralWorkspaceViewModel General { get; }

    public SandboxWorkspaceViewModel Sandbox { get; }

    public ModsAndMapsWorkspaceViewModel ModsAndMaps { get; }

    public NetworkAndAdminWorkspaceViewModel NetworkAndAdmin { get; }

    public BackupsWorkspaceViewModel Backups { get; }

    public LogsWorkspaceViewModel Logs { get; }

    public AdvancedFilesWorkspaceViewModel AdvancedFiles { get; }

    public string SelectedProfileSummary => SelectedProfile is null
        ? "No profile selected yet."
        : $"{SelectedProfile.DisplayName} | {SelectedProfile.Branch} | {SelectedProfile.RuntimeState}";

    public string SelectedProfileBranchSummary => SelectedProfile?.Branch ?? "No branch selected";

    public string SelectedProfileRuntimeSummary => SelectedProfile?.RuntimeState ?? "No runtime state";

    public string SelectedProfilePathSummary => SelectedProfile is null
        ? "No install or cache path selected."
        : $"{SelectedProfile.InstallDirectory} | {SelectedProfile.CacheDirectory}";

    public string SelectedProfilePortsSummary => SelectedProfile is null
        ? "Ports unavailable."
        : $"TCP {SelectedProfile.EditableDefaultPort} | UDP {SelectedProfile.EditableUdpPort} | RCON {SelectedProfile.EditableRconPort}";

    public string SelectedCommunitySummary => SelectedProfile?.CommunitySummary ?? "Select a profile to see its community posture.";

    public string SelectedNetworkSummary => SelectedProfile?.NetworkSummary ?? "Select a profile to see its network and trust posture.";

    public string SelectedWorldSummary => SelectedProfile?.WorldSummary ?? "Select a profile to see its sandbox snapshot.";

    public string SelectedDeploymentSummary => SelectedProfile?.InstallPreflightSummary ?? "Select a profile to see its deployment preflight.";

    public string SelectedLaunchSummary => SelectedProfile?.LaunchReadinessSummary ?? "Select a profile to see its launch readiness.";

    public string SelectedRecoverySummary => SelectedProfile?.BackupSafetySummary ?? "Select a profile to see its recovery posture.";

    public string SelectedWelcomeSummary => SelectedProfile?.WelcomeSummary ?? "No welcome message summary available.";

    public int InstalledProfileCount => Legacy.Profiles.Count(profile => profile.IsInstallDetected);

    public int RecoveryReadyProfileCount => Legacy.Profiles.Count(profile => profile.HasBackup);

    public int DirectJavaReadyProfileCount => Legacy.Profiles.Count(profile => profile.UsesDirectJavaTemplate);

    public int RunningProfileCount => Legacy.Profiles.Count(profile => string.Equals(profile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase));

    public int FallbackLaunchProfileCount => Legacy.Profiles.Count(profile => profile.LauncherDetected && !profile.UsesDirectJavaTemplate);

    public string SelectedWorkspaceSummary => SelectedProfile is null
        ? "Select a profile to unlock the workspace rail."
        : $"This workspace is centered on {SelectedProfile.DisplayName}. The section rail below keeps all profile-specific tasks in one place.";

    public string SelectedWorkspaceAction => SelectedProfile is null
        ? HasProfiles
            ? "Choose a roster entry on the left to open its control stack."
            : "Create or import the first profile, then use the section rail to continue."
        : Directory.Exists(SelectedProfile.InstallDirectory)
            ? "The profile appears installed. Start in Overview, then move to Install & Update or General depending on what needs attention."
            : "The profile is not installed yet. Use Install & Update first, then return here to continue configuration.";

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool HasNoSelectedProfile => !HasSelectedProfile;

    public bool HasUnsavedChanges => CurrentSection is IWorkspaceDirtyState dirtyState && dirtyState.HasUnsavedChanges;

    public string DirtyStateMessage => CurrentSection is IWorkspaceDirtyState dirtyState
        ? dirtyState.DirtyStateMessage
        : "No unsaved section changes.";

    [ObservableProperty]
    private ProfileCardViewModel? selectedProfile;

    [ObservableProperty]
    private ViewModelBase currentSection = null!;

    [ObservableProperty]
    private bool hasPendingSectionNavigation;

    [ObservableProperty]
    private string pendingSectionNavigationMessage = string.Empty;

    [ObservableProperty]
    private WorkspaceNavigationItemViewModel? pendingSectionTarget;

    public IAsyncRelayCommand<WorkspaceNavigationItemViewModel> SelectSectionCommand { get; }

    public IAsyncRelayCommand SaveCurrentDraftCommand { get; }

    public IAsyncRelayCommand DiscardCurrentDraftCommand { get; }

    public IAsyncRelayCommand ConfirmSectionNavigationSaveCommand { get; }

    public IAsyncRelayCommand ConfirmSectionNavigationDiscardCommand { get; }

    public IRelayCommand CancelSectionNavigationCommand { get; }

    public void ApplyBootstrap(IReadOnlyList<WorkspacePageDto> profilePages)
    {
        foreach (var item in SectionItems)
        {
            var page = profilePages.FirstOrDefault(candidate => string.Equals(candidate.Id, item.Key, StringComparison.Ordinal));
            item.IsEnabled = page?.IsEnabled ?? item.IsEnabled;
        }
    }

    public void NavigateToProfile(string profileId, string pageId)
    {
        if (Legacy.Profiles.Count == 0)
        {
            return;
        }

        var targetProfile = Legacy.Profiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal))
            ?? Legacy.Profiles[0];
        SelectedProfile = targetProfile;

        var targetSection = SectionItems.FirstOrDefault(item => string.Equals(item.Key, pageId, StringComparison.Ordinal) && item.IsEnabled)
            ?? SectionItems.FirstOrDefault(item => string.Equals(item.Key, ProfileWorkspacePageIds.Overview, StringComparison.Ordinal))
            ?? SectionItems.First();

        _ = SelectSectionAsync(targetSection);
    }

    public async Task SaveDraftAsync()
    {
        if (CurrentSection is IWorkspaceDirtyState dirtyState)
        {
            await dirtyState.SaveDraftAsync();
            OnPropertyChanged(nameof(DirtyStateMessage));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public async Task DiscardDraftAsync()
    {
        if (CurrentSection is IWorkspaceDirtyState dirtyState)
        {
            await dirtyState.DiscardDraftAsync();
            OnPropertyChanged(nameof(DirtyStateMessage));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    public async Task RefreshPageAsync()
    {
        RefreshWorkspaceState();
        await RefreshCurrentSectionAsync();
    }

    private async Task SelectSectionAsync(WorkspaceNavigationItemViewModel? section)
    {
        if (section is null || !section.IsEnabled)
        {
            return;
        }

        var next = ResolvePage(section.Key);
        if (next is null)
        {
            return;
        }

        if (CurrentSection is IWorkspaceDirtyState dirtyState && dirtyState.HasUnsavedChanges)
        {
            PendingSectionTarget = section;
            PendingSectionNavigationMessage = ReferenceEquals(next, CurrentSection)
                ? $"Save or discard changes in {((IWorkspacePageHeader)CurrentSection).PageTitle} before refreshing it."
                : $"Save or discard changes in {((IWorkspacePageHeader)CurrentSection).PageTitle} before switching to {section.Title}.";
            HasPendingSectionNavigation = true;
            return;
        }

        await SwitchSectionAsync(section.Key, next);
    }

    private async Task ConfirmSectionNavigationSaveAsync()
    {
        if (!HasPendingSectionNavigation)
        {
            return;
        }

        await SaveDraftAsync();
        var target = PendingSectionTarget;
        ClearPendingSectionNavigation();
        if (target is not null)
        {
            var next = ResolvePage(target.Key);
            if (next is not null)
            {
                await SwitchSectionAsync(target.Key, next);
            }
        }
    }

    private async Task ConfirmSectionNavigationDiscardAsync()
    {
        if (!HasPendingSectionNavigation)
        {
            return;
        }

        await DiscardDraftAsync();
        var target = PendingSectionTarget;
        ClearPendingSectionNavigation();
        if (target is not null)
        {
            var next = ResolvePage(target.Key);
            if (next is not null)
            {
                await SwitchSectionAsync(target.Key, next);
            }
        }
    }

    private void CancelSectionNavigation()
    {
        ClearPendingSectionNavigation();
    }

    private ViewModelBase? ResolvePage(string pageId) =>
        _sections.TryGetValue(pageId, out var page)
            ? page
            : null;

    private async Task RefreshCurrentSectionAsync()
    {
        if (CurrentSection is IWorkspaceRefreshable refreshable)
        {
            await refreshable.RefreshPageAsync();
        }
    }

    private async Task SwitchSectionAsync(string pageId, ViewModelBase section)
    {
        if (!ReferenceEquals(CurrentSection, section))
        {
            CurrentSection = section;
            UpdateSectionSelection(pageId);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(DirtyStateMessage));
        }
        else
        {
            UpdateSectionSelection(pageId);
        }

        await Legacy.RefreshCommand.ExecuteAsync(null);
        RefreshWorkspaceState();
        await RefreshCurrentSectionAsync();
    }

    private void UpdateSectionSelection(string selectedPageId)
    {
        foreach (var navItem in SectionItems)
        {
            navItem.IsSelected = string.Equals(navItem.Key, selectedPageId, StringComparison.Ordinal);
        }
    }

    private void ClearPendingSectionNavigation()
    {
        PendingSectionTarget = null;
        PendingSectionNavigationMessage = string.Empty;
        HasPendingSectionNavigation = false;
    }

    partial void OnCurrentSectionChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(DirtyStateMessage));
        OnPropertyChanged(nameof(SelectedWorkspaceSummary));
        OnPropertyChanged(nameof(SelectedWorkspaceAction));
    }

    partial void OnSelectedProfileChanged(ProfileCardViewModel? value)
    {
        _selectedProfileId = value?.ProfileId;
        foreach (var profile in Legacy.Profiles)
        {
            profile.IsSelected = ReferenceEquals(profile, value);
        }

        OnPropertyChanged(nameof(SelectedProfileSummary));
        OnPropertyChanged(nameof(SelectedProfileBranchSummary));
        OnPropertyChanged(nameof(SelectedProfileRuntimeSummary));
        OnPropertyChanged(nameof(SelectedProfilePathSummary));
        OnPropertyChanged(nameof(SelectedProfilePortsSummary));
        OnPropertyChanged(nameof(SelectedCommunitySummary));
        OnPropertyChanged(nameof(SelectedNetworkSummary));
        OnPropertyChanged(nameof(SelectedWorldSummary));
        OnPropertyChanged(nameof(SelectedDeploymentSummary));
        OnPropertyChanged(nameof(SelectedLaunchSummary));
        OnPropertyChanged(nameof(SelectedRecoverySummary));
        OnPropertyChanged(nameof(SelectedWelcomeSummary));
        OnPropertyChanged(nameof(SelectedWorkspaceSummary));
        OnPropertyChanged(nameof(SelectedWorkspaceAction));
        OnPropertyChanged(nameof(WorkspaceHeadline));
        OnPropertyChanged(nameof(WorkspaceGuidance));
        OnPropertyChanged(nameof(HasSelectedProfile));
        OnPropertyChanged(nameof(HasNoSelectedProfile));

        foreach (var page in _sections.Values.OfType<IProfileWorkspacePage>())
        {
            page.SetSelectedProfile(value);
        }

        RefreshWorkspaceState();
    }

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Legacy.Profiles.Count == 0)
        {
            SelectedProfile = null;
        }
        else if (!string.IsNullOrWhiteSpace(_selectedProfileId))
        {
            SelectedProfile = Legacy.Profiles.FirstOrDefault(profile => string.Equals(profile.ProfileId, _selectedProfileId, StringComparison.Ordinal))
                ?? Legacy.Profiles[0];
        }
        else if (SelectedProfile is null)
        {
            SelectedProfile = Legacy.Profiles[0];
        }

        foreach (var profile in Legacy.Profiles)
        {
            profile.IsSelected = ReferenceEquals(profile, SelectedProfile);
        }

        OnPropertyChanged(nameof(ProfileCountSummary));
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
        OnPropertyChanged(nameof(HasImportCandidates));
        OnPropertyChanged(nameof(ImportCandidateCount));
        OnPropertyChanged(nameof(FirstRunHeadline));
        OnPropertyChanged(nameof(FirstRunActionPlan));
        OnPropertyChanged(nameof(FirstRunStepOne));
        OnPropertyChanged(nameof(FirstRunStepTwo));
        OnPropertyChanged(nameof(FirstRunStepThree));
        OnPropertyChanged(nameof(InstalledProfileCount));
        OnPropertyChanged(nameof(RecoveryReadyProfileCount));
        OnPropertyChanged(nameof(DirectJavaReadyProfileCount));
        OnPropertyChanged(nameof(RunningProfileCount));
        OnPropertyChanged(nameof(FallbackLaunchProfileCount));
        OnPropertyChanged(nameof(WorkspaceHeadline));
        OnPropertyChanged(nameof(WorkspaceGuidance));
    }

    private void OnImportCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasImportCandidates));
        OnPropertyChanged(nameof(ImportCandidateCount));
        OnPropertyChanged(nameof(FirstRunHeadline));
        OnPropertyChanged(nameof(FirstRunActionPlan));
        OnPropertyChanged(nameof(FirstRunStepOne));
        OnPropertyChanged(nameof(FirstRunStepTwo));
        OnPropertyChanged(nameof(FirstRunStepThree));
        OnPropertyChanged(nameof(SelectedWorkspaceAction));
    }

    private void RefreshWorkspaceState()
    {
        OnPropertyChanged(nameof(ProfileCountSummary));
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
        OnPropertyChanged(nameof(HasImportCandidates));
        OnPropertyChanged(nameof(ImportCandidateCount));
        OnPropertyChanged(nameof(FirstRunHeadline));
        OnPropertyChanged(nameof(FirstRunActionPlan));
        OnPropertyChanged(nameof(FirstRunStepOne));
        OnPropertyChanged(nameof(FirstRunStepTwo));
        OnPropertyChanged(nameof(FirstRunStepThree));
        OnPropertyChanged(nameof(InstalledProfileCount));
        OnPropertyChanged(nameof(RecoveryReadyProfileCount));
        OnPropertyChanged(nameof(DirectJavaReadyProfileCount));
        OnPropertyChanged(nameof(RunningProfileCount));
        OnPropertyChanged(nameof(FallbackLaunchProfileCount));
        OnPropertyChanged(nameof(SelectedProfileSummary));
        OnPropertyChanged(nameof(SelectedProfileBranchSummary));
        OnPropertyChanged(nameof(SelectedProfileRuntimeSummary));
        OnPropertyChanged(nameof(SelectedProfilePathSummary));
        OnPropertyChanged(nameof(SelectedProfilePortsSummary));
        OnPropertyChanged(nameof(SelectedCommunitySummary));
        OnPropertyChanged(nameof(SelectedNetworkSummary));
        OnPropertyChanged(nameof(SelectedWorldSummary));
        OnPropertyChanged(nameof(SelectedDeploymentSummary));
        OnPropertyChanged(nameof(SelectedLaunchSummary));
        OnPropertyChanged(nameof(SelectedRecoverySummary));
        OnPropertyChanged(nameof(SelectedWelcomeSummary));
        OnPropertyChanged(nameof(SelectedWorkspaceSummary));
        OnPropertyChanged(nameof(SelectedWorkspaceAction));
        OnPropertyChanged(nameof(WorkspaceHeadline));
        OnPropertyChanged(nameof(WorkspaceGuidance));
        OnPropertyChanged(nameof(HasSelectedProfile));
        OnPropertyChanged(nameof(HasNoSelectedProfile));
    }
}
