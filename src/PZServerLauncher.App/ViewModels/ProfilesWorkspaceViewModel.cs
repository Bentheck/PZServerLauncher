using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PZServerLauncher.App.ViewModels;

public partial class ProfilesWorkspaceViewModel : ViewModelBase, IWorkspacePageHeader, IWorkspaceDirtyState
{
    private readonly List<WorkspaceSectionViewModel> _sections;
    private WorkspaceSectionViewModel _currentSection = null!;

    public ProfilesWorkspaceViewModel(MainWindowViewModel legacy, Action openClassicAction)
    {
        Legacy = legacy;

        Sections =
        [
            new WorkspaceSectionViewModel(
                "Overview",
                "Runtime state, branch/build, install state, backup summary, and quick actions for the selected profile.",
                "Overview draft cleared.",
                ["Runtime state", "Install state", "Backup summary", "Quick actions"]),
            new WorkspaceSectionViewModel(
                "Install & Update",
                "Branch selection, install directory, preflight checks, and streamed update progress.",
                "Install & Update draft cleared.",
                ["Branch selection", "Install directory", "Preflight checks", "Job progress"]),
            new WorkspaceSectionViewModel(
                "General",
                "Structured server identity, memory, startup behavior, and primary ports.",
                "General draft cleared.",
                ["Server name", "Memory", "Startup behavior", "Primary ports"]),
            new WorkspaceSectionViewModel(
                "Sandbox",
                "Near-full structured gameplay and world settings for the selected branch.",
                "Sandbox draft cleared.",
                ["Branch-specific catalogs", "Gameplay settings", "World settings", "Fallback to raw files when needed"]),
            new WorkspaceSectionViewModel(
                "Mods & Maps",
                "Workshop items, local mod discovery, map ordering, presets, and validation.",
                "Mods & Maps draft cleared.",
                ["Workshop IDs", "Local mod discovery", "Map validation", "Named presets"]),
            new WorkspaceSectionViewModel(
                "Network & Admin",
                "Bind address, admin/RCON settings, and access/security-related server knobs.",
                "Network & Admin draft cleared.",
                ["Bind IP", "RCON", "Admin settings", "Security options"]),
            new WorkspaceSectionViewModel(
                "Backups",
                "Backup history, manual backups, restore flow, and retention hints.",
                "Backups draft cleared.",
                ["Manual backups", "Restore flow", "Retention", "Recent backups"]),
            new WorkspaceSectionViewModel(
                "Logs",
                "Live runtime output and recent server messages for the selected profile.",
                "Logs draft cleared.",
                ["Live stream", "Recent messages", "Runtime status", "Host messages"]),
            new WorkspaceSectionViewModel(
                "Advanced Files",
                "Raw ini, SandboxVars, spawnregions, and spawnpoints editing for unsupported or advanced cases.",
                "Advanced Files draft cleared.",
                ["Raw ini", "SandboxVars.lua", "SpawnRegions.lua", "SpawnPoints.lua"]),
        ];

        _currentSection = Sections[0];
        _sections = Sections.ToList();
        SectionItems = _sections.Select(section => new WorkspaceNavigationItemViewModel(section.PageTitle, section.PageTitle, section.PageSummary)).ToList();
        CurrentSection = _currentSection;
        SelectedProfile = Profiles.FirstOrDefault();
        UpdateSectionSelection(_currentSection);

        SelectSectionCommand = new RelayCommand<WorkspaceNavigationItemViewModel>(SelectSection);
        OpenClassicCommand = new RelayCommand(openClassicAction);
        SaveCurrentDraftCommand = new RelayCommand(SaveDraft);
        DiscardCurrentDraftCommand = new RelayCommand(DiscardDraft);
        ConfirmSectionNavigationSaveCommand = new RelayCommand(ConfirmSectionNavigationSave);
        ConfirmSectionNavigationDiscardCommand = new RelayCommand(ConfirmSectionNavigationDiscard);
        CancelSectionNavigationCommand = new RelayCommand(CancelSectionNavigation);
    }

    public MainWindowViewModel Legacy { get; }

    public string PageTitle => "Profiles";

    public string PageSummary => "Choose a profile, then use the per-profile workspace sections. Classic remains available from the main shell during migration.";

    public ObservableCollection<WorkspaceSectionViewModel> Sections { get; }

    public IReadOnlyList<WorkspaceNavigationItemViewModel> SectionItems { get; }

    public IReadOnlyList<ProfileCardViewModel> Profiles => Legacy.Profiles;

    public string SelectedProfileSummary => SelectedProfile is null
        ? "No profile selected yet."
        : $"{SelectedProfile.DisplayName} | {SelectedProfile.Branch} | {SelectedProfile.RuntimeState}";

    [ObservableProperty]
    private ProfileCardViewModel? selectedProfile;

    [ObservableProperty]
    private WorkspaceSectionViewModel currentSection = null!;

    [ObservableProperty]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    private string dirtyStateMessage = "No unsaved changes.";

    [ObservableProperty]
    private bool hasPendingSectionNavigation;

    [ObservableProperty]
    private string pendingSectionNavigationMessage = string.Empty;

    [ObservableProperty]
    private WorkspaceSectionViewModel? pendingSection;

    public IRelayCommand<WorkspaceNavigationItemViewModel> SelectSectionCommand { get; }

    public IRelayCommand OpenClassicCommand { get; }

    public IRelayCommand SaveCurrentDraftCommand { get; }

    public IRelayCommand DiscardCurrentDraftCommand { get; }

    public IRelayCommand ConfirmSectionNavigationSaveCommand { get; }

    public IRelayCommand ConfirmSectionNavigationDiscardCommand { get; }

    public IRelayCommand CancelSectionNavigationCommand { get; }

    public void SaveDraft()
    {
        CurrentSection.SaveDraft();
        SyncDirtyState();
    }

    public void DiscardDraft()
    {
        CurrentSection.DiscardDraft();
        SyncDirtyState();
    }

    public void SelectSection(WorkspaceNavigationItemViewModel? section)
    {
        if (section is null)
        {
            return;
        }

        var next = _sections.FirstOrDefault(x => string.Equals(x.PageTitle, section.Key, StringComparison.OrdinalIgnoreCase));
        if (next is null || ReferenceEquals(next, CurrentSection))
        {
            return;
        }

        if (CurrentSection.HasUnsavedChanges)
        {
            PendingSection = next;
            PendingSectionNavigationMessage = $"Save or discard changes in {CurrentSection.PageTitle} before switching to {next.PageTitle}.";
            HasPendingSectionNavigation = true;
            return;
        }

        SwitchSection(next);
    }

    private void ConfirmSectionNavigationSave()
    {
        if (!HasPendingSectionNavigation)
        {
            return;
        }

        CurrentSection.SaveDraft();
        var next = PendingSection;
        ClearPendingSectionNavigation();

        if (next is not null)
        {
            SwitchSection(next);
        }
    }

    private void ConfirmSectionNavigationDiscard()
    {
        if (!HasPendingSectionNavigation)
        {
            return;
        }

        CurrentSection.DiscardDraft();
        var next = PendingSection;
        ClearPendingSectionNavigation();

        if (next is not null)
        {
            SwitchSection(next);
        }
    }

    private void CancelSectionNavigation()
    {
        ClearPendingSectionNavigation();
    }

    private void SwitchSection(WorkspaceSectionViewModel next)
    {
        CurrentSection = next;
        UpdateSectionSelection(next);
        SyncDirtyState();
    }

    private void UpdateSectionSelection(WorkspaceSectionViewModel section)
    {
        foreach (var navItem in SectionItems)
        {
            navItem.IsSelected = string.Equals(navItem.Title, section.PageTitle, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SyncDirtyState()
    {
        HasUnsavedChanges = CurrentSection.HasUnsavedChanges;
        DirtyStateMessage = CurrentSection.DirtyStateMessage;
    }

    private void ClearPendingSectionNavigation()
    {
        PendingSection = null;
        PendingSectionNavigationMessage = string.Empty;
        HasPendingSectionNavigation = false;
    }

    partial void OnCurrentSectionChanged(WorkspaceSectionViewModel value)
    {
        SyncDirtyState();
    }

    partial void OnSelectedProfileChanged(ProfileCardViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedProfileSummary));
    }
}
