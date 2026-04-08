using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;

namespace PZServerLauncher.App.ViewModels;

public partial class WorkspaceShellViewModel : ViewModelBase, IWorkspacePageHeader
{
    private readonly DesktopShellService _desktopShellService;

    public WorkspaceShellViewModel()
        : this(new MainWindowViewModel(), new DesktopShellService())
    {
    }

    public WorkspaceShellViewModel(MainWindowViewModel legacy, DesktopShellService desktopShellService)
    {
        Legacy = legacy;
        _desktopShellService = desktopShellService;

        Dashboard = new WorkspaceSectionViewModel(
            "Dashboard",
            "High-level status, quick navigation, and migration guidance while the workspace shell is still landing.",
            "Dashboard draft cleared.",
            ["Runtime summary", "Latest jobs", "Host status", "Migration guidance"]);
        Host = new WorkspaceSectionViewModel(
            "Host",
            "Host settings, host lifecycle controls, and startup behavior are grouped here during the migration.",
            "Host draft cleared.",
            ["Start with Windows", "Stop host", "Start/stop all", "Host health"]);
        RemoteAccess = new WorkspaceSectionViewModel(
            "Remote Access",
            "Optional HTTPS admin setup, firewall guidance, and self-test scaffolding live here.",
            "Remote Access draft cleared.",
            ["HTTPS binding", "Firewall rule", "Self-test", "Remote admin"]);
        Users = new WorkspaceSectionViewModel(
            "Users",
            "Owner bootstrap and the future web-role surface will land here.",
            "Users draft cleared.",
            ["Owner bootstrap", "Admin roles", "Operator roles", "Viewer roles"]);
        Profiles = new ProfilesWorkspaceViewModel(legacy, () => SelectGlobalPageByKey(nameof(Classic)));
        Classic = new ClassicWorkspaceViewModel(legacy);

        GlobalNavigation =
        [
            new WorkspaceNavigationItemViewModel(nameof(Dashboard), Dashboard.PageTitle, Dashboard.PageSummary),
            new WorkspaceNavigationItemViewModel(nameof(Profiles), Profiles.PageTitle, Profiles.PageSummary),
            new WorkspaceNavigationItemViewModel(nameof(Host), Host.PageTitle, Host.PageSummary),
            new WorkspaceNavigationItemViewModel(nameof(RemoteAccess), RemoteAccess.PageTitle, RemoteAccess.PageSummary),
            new WorkspaceNavigationItemViewModel(nameof(Users), Users.PageTitle, Users.PageSummary),
        ];

        CurrentPage = Dashboard;
        MarkNavigationSelection(nameof(Dashboard));

        SelectGlobalPageCommand = new RelayCommand<WorkspaceNavigationItemViewModel>(SelectGlobalPage);
        SaveCurrentDraftCommand = new RelayCommand(SaveCurrentDraft);
        DiscardCurrentDraftCommand = new RelayCommand(DiscardCurrentDraft);
        ConfirmNavigationSaveCommand = new RelayCommand(ConfirmNavigationSave);
        ConfirmNavigationDiscardCommand = new RelayCommand(ConfirmNavigationDiscard);
        CancelNavigationCommand = new RelayCommand(CancelNavigation);
        OpenClassicCommand = new RelayCommand(() => SelectGlobalPageByKey(nameof(Classic)));
        ExitDesktopCommand = new RelayCommand(() => _desktopShellService.ExitDesktop());
        RefreshLegacyCommand = new AsyncRelayCommand(() => Legacy.RefreshCommand.ExecuteAsync(null));
    }

    public MainWindowViewModel Legacy { get; }

    public string PageTitle => "Project Zomboid Workspace";

    public string PageSummary => "Workspace shell scaffold with dashboard, profiles, host, remote access, users, and a temporary Classic surface.";

    public string CurrentPageTitle => CurrentPage is IWorkspacePageHeader header ? header.PageTitle : PageTitle;

    public string CurrentPageSummary => CurrentPage is IWorkspacePageHeader header ? header.PageSummary : PageSummary;

    public ObservableCollection<WorkspaceNavigationItemViewModel> GlobalNavigation { get; }

    public WorkspaceSectionViewModel Dashboard { get; }

    public ProfilesWorkspaceViewModel Profiles { get; }

    public WorkspaceSectionViewModel Host { get; }

    public WorkspaceSectionViewModel RemoteAccess { get; }

    public WorkspaceSectionViewModel Users { get; }

    public ClassicWorkspaceViewModel Classic { get; }

    [ObservableProperty]
    private ViewModelBase currentPage = null!;

    [ObservableProperty]
    private string pageStatus = "Workspace shell is ready.";

    [ObservableProperty]
    private string pendingNavigationMessage = string.Empty;

    [ObservableProperty]
    private bool hasPendingNavigation;

    [ObservableProperty]
    private WorkspaceNavigationItemViewModel? pendingNavigationTarget;

    public IRelayCommand<WorkspaceNavigationItemViewModel> SelectGlobalPageCommand { get; }

    public IRelayCommand SaveCurrentDraftCommand { get; }

    public IRelayCommand DiscardCurrentDraftCommand { get; }

    public IRelayCommand ConfirmNavigationSaveCommand { get; }

    public IRelayCommand ConfirmNavigationDiscardCommand { get; }

    public IRelayCommand CancelNavigationCommand { get; }

    public IRelayCommand OpenClassicCommand { get; }

    public IRelayCommand ExitDesktopCommand { get; }

    public IAsyncRelayCommand RefreshLegacyCommand { get; }

    public void SelectGlobalPage(WorkspaceNavigationItemViewModel? target)
    {
        if (target is null)
        {
            return;
        }

        SelectGlobalPageByKey(target.Key);
    }

    public void SelectGlobalPageByKey(string key)
    {
        var next = ResolvePage(key);
        if (next is null || ReferenceEquals(next, CurrentPage))
        {
            return;
        }

        if (CurrentPage is IWorkspaceDirtyState dirtyState && dirtyState.HasUnsavedChanges)
        {
            PendingNavigationTarget = GlobalNavigation.FirstOrDefault(item => item.Key == key);
            PendingNavigationMessage = $"Save or discard changes in {((IWorkspacePageHeader)CurrentPage).PageTitle} before switching to {((IWorkspacePageHeader)next).PageTitle}.";
            HasPendingNavigation = true;
            return;
        }

        NavigateTo(next, key);
    }

    public void SaveCurrentDraft()
    {
        if (CurrentPage is IWorkspaceDirtyState dirtyState)
        {
            dirtyState.SaveDraftAsync().GetAwaiter().GetResult();
            UpdateCurrentStatus();
        }
    }

    public void DiscardCurrentDraft()
    {
        if (CurrentPage is IWorkspaceDirtyState dirtyState)
        {
            dirtyState.DiscardDraftAsync().GetAwaiter().GetResult();
            UpdateCurrentStatus();
        }
    }

    private void ConfirmNavigationSave()
    {
        if (!HasPendingNavigation)
        {
            return;
        }

        SaveCurrentDraft();
        var next = PendingNavigationTarget;
        ClearPendingNavigation();
        if (next is not null)
        {
            NavigateTo(ResolvePage(next.Key) ?? Dashboard, next.Key);
        }
    }

    private void ConfirmNavigationDiscard()
    {
        if (!HasPendingNavigation)
        {
            return;
        }

        DiscardCurrentDraft();
        var next = PendingNavigationTarget;
        ClearPendingNavigation();
        if (next is not null)
        {
            NavigateTo(ResolvePage(next.Key) ?? Dashboard, next.Key);
        }
    }

    private void CancelNavigation()
    {
        ClearPendingNavigation();
    }

    private void NavigateTo(ViewModelBase next, string key)
    {
        CurrentPage = next;
        MarkNavigationSelection(key);
        UpdateCurrentStatus();
    }

    private ViewModelBase? ResolvePage(string key) =>
        key switch
        {
            nameof(Dashboard) => Dashboard,
            nameof(Profiles) => Profiles,
            nameof(Host) => Host,
            nameof(RemoteAccess) => RemoteAccess,
            nameof(Users) => Users,
            nameof(Classic) => Classic,
            _ => null,
        };

    private void MarkNavigationSelection(string key)
    {
        foreach (var item in GlobalNavigation)
        {
            item.IsSelected = string.Equals(item.Key, key, StringComparison.Ordinal);
        }
    }

    private void UpdateCurrentStatus()
    {
        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentPageSummary));
        PageStatus = CurrentPageSummary;
    }

    private void ClearPendingNavigation()
    {
        PendingNavigationTarget = null;
        PendingNavigationMessage = string.Empty;
        HasPendingNavigation = false;
    }

    partial void OnCurrentPageChanged(ViewModelBase value)
    {
        UpdateCurrentStatus();
    }
}
