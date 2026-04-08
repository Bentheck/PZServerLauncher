using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class ProfilesWorkspaceViewModel : ViewModelBase, IWorkspacePageHeader, IWorkspaceDirtyState
{
    private readonly IReadOnlyDictionary<string, ViewModelBase> _sections;

    public ProfilesWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient, RuntimeEventStream runtimeEventStream)
    {
        Legacy = legacy;
        if (Legacy.Profiles is INotifyCollectionChanged profiles)
        {
            profiles.CollectionChanged += OnProfilesChanged;
        }

        Overview = new OverviewWorkspaceViewModel(legacy);
        InstallAndUpdate = new InstallUpdateWorkspaceViewModel(legacy);
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

        SelectSectionCommand = new RelayCommand<WorkspaceNavigationItemViewModel>(SelectSection);
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
        : $"TCP {SelectedProfile.DefaultPort} | UDP {SelectedProfile.UdpPort} | RCON {SelectedProfile.RconPort}";

    public string SelectedWorkspaceSummary => SelectedProfile is null
        ? "Select a profile to unlock the workspace rail."
        : $"This workspace is centered on {SelectedProfile.DisplayName}. The section rail below keeps all profile-specific tasks in one place.";

    public string SelectedWorkspaceAction => SelectedProfile is null
        ? "Import or create a profile, then use the section rail to continue."
        : Directory.Exists(SelectedProfile.InstallDirectory)
            ? "The profile appears installed. Start in Overview, then move to Install & Update or General depending on what needs attention."
            : "The profile is not installed yet. Use Install & Update first, then return here to continue configuration.";

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

    public IRelayCommand<WorkspaceNavigationItemViewModel> SelectSectionCommand { get; }

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

    private void SelectSection(WorkspaceNavigationItemViewModel? section)
    {
        if (section is null || !section.IsEnabled)
        {
            return;
        }

        var next = ResolvePage(section.Key);
        if (next is null || ReferenceEquals(next, CurrentSection))
        {
            return;
        }

        if (CurrentSection is IWorkspaceDirtyState dirtyState && dirtyState.HasUnsavedChanges)
        {
            PendingSectionTarget = section;
            PendingSectionNavigationMessage = $"Save or discard changes in {((IWorkspacePageHeader)CurrentSection).PageTitle} before switching to {section.Title}.";
            HasPendingSectionNavigation = true;
            return;
        }

        SwitchSection(section.Key, next);
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
                SwitchSection(target.Key, next);
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
                SwitchSection(target.Key, next);
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

    private void SwitchSection(string pageId, ViewModelBase section)
    {
        CurrentSection = section;
        UpdateSectionSelection(pageId);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(DirtyStateMessage));
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
        OnPropertyChanged(nameof(SelectedProfileSummary));
        OnPropertyChanged(nameof(SelectedProfileBranchSummary));
        OnPropertyChanged(nameof(SelectedProfileRuntimeSummary));
        OnPropertyChanged(nameof(SelectedProfilePathSummary));
        OnPropertyChanged(nameof(SelectedProfilePortsSummary));
        OnPropertyChanged(nameof(SelectedWorkspaceSummary));
        OnPropertyChanged(nameof(SelectedWorkspaceAction));
        OnPropertyChanged(nameof(WorkspaceHeadline));
        OnPropertyChanged(nameof(WorkspaceGuidance));

        foreach (var page in _sections.Values.OfType<IProfileWorkspacePage>())
        {
            page.SetSelectedProfile(value);
        }
    }

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ProfileCountSummary));
        OnPropertyChanged(nameof(WorkspaceHeadline));
        OnPropertyChanged(nameof(WorkspaceGuidance));
    }
}
