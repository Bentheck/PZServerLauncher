using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class WorkspaceShellViewModel : ViewModelBase, IWorkspacePageHeader
{
    private readonly DesktopShellService _desktopShellService;
    private readonly LocalHostApiClient _hostApiClient;

    public WorkspaceShellViewModel()
        : this(new MainWindowViewModel(), new LocalHostApiClient(), new RuntimeEventStream(new LocalHostApiClient()), new DesktopShellService())
    {
    }

    public WorkspaceShellViewModel(
        MainWindowViewModel legacy,
        LocalHostApiClient hostApiClient,
        RuntimeEventStream runtimeEventStream,
        DesktopShellService desktopShellService)
    {
        Legacy = legacy;
        _hostApiClient = hostApiClient;
        _desktopShellService = desktopShellService;

        Dashboard = new DashboardWorkspaceViewModel(legacy);
        Host = new HostWorkspaceViewModel(legacy);
        RemoteAccess = new RemoteAccessWorkspaceViewModel(legacy);
        Users = new UsersWorkspaceViewModel(legacy, hostApiClient);
        Profiles = new ProfilesWorkspaceViewModel(legacy, hostApiClient, runtimeEventStream);

        _pages = new Dictionary<string, ViewModelBase>(StringComparer.Ordinal)
        {
            [WorkspacePageIds.Dashboard] = Dashboard,
            [WorkspacePageIds.Profiles] = Profiles,
            [WorkspacePageIds.Host] = Host,
            [WorkspacePageIds.RemoteAccess] = RemoteAccess,
            [WorkspacePageIds.Users] = Users,
        };

        GlobalNavigation =
        [
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Dashboard, "Dashboard", Dashboard.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Profiles, "Profiles", Profiles.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Host, "Host", Host.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.RemoteAccess, "Remote Access", RemoteAccess.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Users, "Users", Users.PageSummary),
        ];

        CurrentPage = Dashboard;
        MarkNavigationSelection(WorkspacePageIds.Dashboard);

        SelectGlobalPageCommand = new RelayCommand<WorkspaceNavigationItemViewModel>(SelectGlobalPage);
        SaveCurrentDraftCommand = new AsyncRelayCommand(SaveCurrentDraftAsync);
        DiscardCurrentDraftCommand = new AsyncRelayCommand(DiscardCurrentDraftAsync);
        ConfirmNavigationSaveCommand = new AsyncRelayCommand(ConfirmNavigationSaveAsync);
        ConfirmNavigationDiscardCommand = new AsyncRelayCommand(ConfirmNavigationDiscardAsync);
        CancelNavigationCommand = new RelayCommand(CancelNavigation);
        ExitDesktopCommand = new RelayCommand(() => _desktopShellService.ExitDesktop());
        RefreshLegacyCommand = new AsyncRelayCommand(RefreshAsync);

        _ = InitializeAsync();
    }

    private readonly IReadOnlyDictionary<string, ViewModelBase> _pages;

    public MainWindowViewModel Legacy { get; }

    public string PageTitle => "Project Zomboid Workspace";

    public string PageSummary => "Workspace shell with dedicated dashboard, profiles, host, remote access, and users pages.";

    public string CurrentPageTitle => CurrentPage is IWorkspacePageHeader header ? header.PageTitle : PageTitle;

    public string CurrentPageSummary => CurrentPage is IWorkspacePageHeader header ? header.PageSummary : PageSummary;

    public ObservableCollection<WorkspaceNavigationItemViewModel> GlobalNavigation { get; }

    public DashboardWorkspaceViewModel Dashboard { get; }

    public ProfilesWorkspaceViewModel Profiles { get; }

    public HostWorkspaceViewModel Host { get; }

    public RemoteAccessWorkspaceViewModel RemoteAccess { get; }

    public UsersWorkspaceViewModel Users { get; }

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

    [ObservableProperty]
    private string actorSummary = "Loading workspace capabilities...";

    public IRelayCommand<WorkspaceNavigationItemViewModel> SelectGlobalPageCommand { get; }

    public IAsyncRelayCommand SaveCurrentDraftCommand { get; }

    public IAsyncRelayCommand DiscardCurrentDraftCommand { get; }

    public IAsyncRelayCommand ConfirmNavigationSaveCommand { get; }

    public IAsyncRelayCommand ConfirmNavigationDiscardCommand { get; }

    public IRelayCommand CancelNavigationCommand { get; }

    public IRelayCommand ExitDesktopCommand { get; }

    public IAsyncRelayCommand RefreshLegacyCommand { get; }

    public void SelectGlobalPage(WorkspaceNavigationItemViewModel? target)
    {
        if (target is null || !target.IsEnabled)
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

    private async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await Legacy.RefreshCommand.ExecuteAsync(null);

        try
        {
            var bootstrap = await _hostApiClient.GetWorkspaceBootstrapAsync();
            if (bootstrap is not null)
            {
                ApplyBootstrap(bootstrap);
            }
        }
        catch
        {
        }
    }

    private void ApplyBootstrap(WorkspaceBootstrapDto bootstrap)
    {
        ActorSummary = $"{bootstrap.Actor.DisplayName} | {bootstrap.Actor.Surface} | {bootstrap.Actor.Roles.Count} role(s)";

        foreach (var item in GlobalNavigation)
        {
            var page = bootstrap.GlobalPages.FirstOrDefault(candidate => string.Equals(candidate.Id, item.Key, StringComparison.Ordinal));
            item.IsEnabled = page?.IsEnabled ?? item.IsEnabled;
        }

        Profiles.ApplyBootstrap(bootstrap.ProfilePages);
    }

    private async Task SaveCurrentDraftAsync()
    {
        if (CurrentPage is IWorkspaceDirtyState dirtyState)
        {
            await dirtyState.SaveDraftAsync();
            UpdateCurrentStatus();
        }
    }

    private async Task DiscardCurrentDraftAsync()
    {
        if (CurrentPage is IWorkspaceDirtyState dirtyState)
        {
            await dirtyState.DiscardDraftAsync();
            UpdateCurrentStatus();
        }
    }

    private async Task ConfirmNavigationSaveAsync()
    {
        if (!HasPendingNavigation)
        {
            return;
        }

        await SaveCurrentDraftAsync();
        var next = PendingNavigationTarget;
        ClearPendingNavigation();
        if (next is not null)
        {
            NavigateTo(ResolvePage(next.Key) ?? Dashboard, next.Key);
        }
    }

    private async Task ConfirmNavigationDiscardAsync()
    {
        if (!HasPendingNavigation)
        {
            return;
        }

        await DiscardCurrentDraftAsync();
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
        _pages.TryGetValue(key, out var page)
            ? page
            : null;

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
