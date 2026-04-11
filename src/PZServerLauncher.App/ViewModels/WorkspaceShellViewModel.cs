using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class WorkspaceShellViewModel : ViewModelBase, IWorkspacePageHeader
{
    private readonly DesktopShellService _desktopShellService;
    private readonly LocalHostApiClient _hostApiClient;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public WorkspaceShellViewModel()
        : this(
            new MainWindowViewModel(),
            new LocalHostApiClient(),
            new RuntimeEventStream(new LocalHostApiClient()),
            new DesktopShellService(new DesktopLogService()),
            new FolderPickerService())
    {
    }

    public WorkspaceShellViewModel(
        MainWindowViewModel legacy,
        LocalHostApiClient hostApiClient,
        RuntimeEventStream runtimeEventStream,
        DesktopShellService desktopShellService,
        FolderPickerService folderPickerService)
    {
        Legacy = legacy;
        _hostApiClient = hostApiClient;
        _desktopShellService = desktopShellService;
        Legacy.Profiles.CollectionChanged += OnLegacyCollectionChanged;
        Legacy.RecentJobs.CollectionChanged += OnLegacyCollectionChanged;
        Legacy.WorkspaceNavigationRequested += OnWorkspaceNavigationRequested;

        Dashboard = new DashboardWorkspaceViewModel(
            legacy,
            () => SelectGlobalPageByKey(WorkspacePageIds.Profiles),
            () => SelectGlobalPageByKey(WorkspacePageIds.Users));
        Host = new HostWorkspaceViewModel(
            legacy,
            () => SelectGlobalPageByKey(WorkspacePageIds.Users));
        RemoteAccess = new RemoteAccessWorkspaceViewModel(legacy);
        Users = new UsersWorkspaceViewModel(legacy, hostApiClient);
        Profiles = new ProfilesWorkspaceViewModel(legacy, hostApiClient, runtimeEventStream, folderPickerService);

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
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Dashboard, "Home", Dashboard.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Profiles, "Servers", Profiles.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Host, "App", Host.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.RemoteAccess, "Web Access", RemoteAccess.PageSummary),
            new WorkspaceNavigationItemViewModel(WorkspacePageIds.Users, "Users", Users.PageSummary),
        ];

        CurrentPage = Dashboard;
        MarkNavigationSelection(WorkspacePageIds.Dashboard);

        SelectGlobalPageCommand = new AsyncRelayCommand<WorkspaceNavigationItemViewModel>(SelectGlobalPageAsync);
        SaveCurrentDraftCommand = new AsyncRelayCommand(SaveCurrentDraftAsync);
        DiscardCurrentDraftCommand = new AsyncRelayCommand(DiscardCurrentDraftAsync);
        ConfirmNavigationSaveCommand = new AsyncRelayCommand(ConfirmNavigationSaveAsync);
        ConfirmNavigationDiscardCommand = new AsyncRelayCommand(ConfirmNavigationDiscardAsync);
        CancelNavigationCommand = new RelayCommand(CancelNavigation);
        ExitDesktopCommand = new RelayCommand(() => _desktopShellService.ExitDesktop());
        RefreshLegacyCommand = new AsyncRelayCommand(RefreshWorkspaceAsync);

        _ = InitializeAsync();
    }

    private readonly IReadOnlyDictionary<string, ViewModelBase> _pages;

    public MainWindowViewModel Legacy { get; }

    public string PageTitle => "Project Zomboid Server Manager";

    public string PageSummary => "Create, configure, launch, and recover local servers from one desktop app.";

    public string CurrentPageTitle => CurrentPage is IWorkspacePageHeader header ? header.PageTitle : PageTitle;

    public string CurrentPageSummary => CurrentPage is IWorkspacePageHeader header ? header.PageSummary : PageSummary;

    public string WorkspaceGuidance => CurrentPage switch
    {
        ProfilesWorkspaceViewModel => "Choose a server, then walk through install, settings, mods, backups, and logs without leaving the same area.",
        DashboardWorkspaceViewModel => "Start here to create a new server, import an existing one, or jump back into the last thing you were doing.",
        HostWorkspaceViewModel => "App settings live here so startup, background host behavior, and machine-wide controls stay in one place.",
        RemoteAccessWorkspaceViewModel => "Web access is optional. Turn it on only when you want to use this machine from a browser.",
        UsersWorkspaceViewModel => "Manage the owner account and any browser users here when web access is enabled.",
        _ => CurrentPageSummary,
    };

    public string ProfileCountSummary => Legacy.Profiles.Count == 0
        ? "No servers yet"
        : $"{Legacy.Profiles.Count} server{(Legacy.Profiles.Count == 1 ? string.Empty : "s")}";

    public string RecentActivitySummary => Legacy.RecentJobs.Count == 0
        ? "No recent activity"
        : $"{Legacy.RecentJobs.Count} recent task{(Legacy.RecentJobs.Count == 1 ? string.Empty : "s")}";

    public string RemotePostureSummary => Legacy.RemoteSummary;

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

    public IAsyncRelayCommand<WorkspaceNavigationItemViewModel> SelectGlobalPageCommand { get; }

    public IAsyncRelayCommand SaveCurrentDraftCommand { get; }

    public IAsyncRelayCommand DiscardCurrentDraftCommand { get; }

    public IAsyncRelayCommand ConfirmNavigationSaveCommand { get; }

    public IAsyncRelayCommand ConfirmNavigationDiscardCommand { get; }

    public IRelayCommand CancelNavigationCommand { get; }

    public IRelayCommand ExitDesktopCommand { get; }

    public IAsyncRelayCommand RefreshLegacyCommand { get; }

    public void SelectGlobalPageByKey(string key)
    {
        _ = SelectGlobalPageByKeyAsync(key);
    }

    private async Task SelectGlobalPageAsync(WorkspaceNavigationItemViewModel? target)
    {
        if (target is null || !target.IsEnabled)
        {
            return;
        }

        await SelectGlobalPageByKeyAsync(target.Key);
    }

    private async Task SelectGlobalPageByKeyAsync(string key)
    {
        var next = ResolvePage(key);
        if (next is null)
        {
            return;
        }

        if (CurrentPage is IWorkspaceDirtyState dirtyState && dirtyState.HasUnsavedChanges)
        {
            PendingNavigationTarget = GlobalNavigation.FirstOrDefault(item => item.Key == key);
            PendingNavigationMessage = ReferenceEquals(next, CurrentPage)
                ? $"Save or discard changes in {((IWorkspacePageHeader)CurrentPage).PageTitle} before refreshing it."
                : $"Save or discard changes in {((IWorkspacePageHeader)CurrentPage).PageTitle} before switching to {((IWorkspacePageHeader)next).PageTitle}.";
            HasPendingNavigation = true;
            return;
        }

        await NavigateToAsync(next, key);
    }

    private async Task InitializeAsync()
    {
        await RefreshWorkspaceAsync();
    }

    private async Task RefreshWorkspaceAsync()
    {
        await RefreshWorkspaceAsync(CurrentPage);
    }

    private async Task RefreshLegacyStateAsync()
    {
        await Legacy.RefreshCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(ProfileCountSummary));
        OnPropertyChanged(nameof(RecentActivitySummary));
        OnPropertyChanged(nameof(RemotePostureSummary));

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

    private static async Task RefreshPageAsync(ViewModelBase page)
    {
        if (page is IWorkspaceRefreshable refreshable)
        {
            await refreshable.RefreshPageAsync();
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
            await NavigateToAsync(ResolvePage(next.Key) ?? Dashboard, next.Key);
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
            await NavigateToAsync(ResolvePage(next.Key) ?? Dashboard, next.Key);
        }
    }

    private void CancelNavigation()
    {
        ClearPendingNavigation();
    }

    private async Task NavigateToAsync(ViewModelBase next, string key)
    {
        if (!ReferenceEquals(CurrentPage, next))
        {
            CurrentPage = next;
            MarkNavigationSelection(key);
            UpdateCurrentStatus();
        }
        else
        {
            MarkNavigationSelection(key);
        }

        await RefreshWorkspaceAsync(next);
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
        OnPropertyChanged(nameof(WorkspaceGuidance));
        PageStatus = CurrentPageSummary;
    }

    private void OnLegacyCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ProfileCountSummary));
        OnPropertyChanged(nameof(RecentActivitySummary));
    }

    private void OnWorkspaceNavigationRequested(object? sender, WorkspaceNavigationRequest request)
    {
        if (string.Equals(request.GlobalPageId, WorkspacePageIds.Profiles, StringComparison.Ordinal))
        {
            SelectGlobalPageByKey(WorkspacePageIds.Profiles);
            if (!string.IsNullOrWhiteSpace(request.ProfileId))
            {
                Profiles.NavigateToProfile(
                    request.ProfileId,
                    request.ProfilePageId ?? ProfileWorkspacePageIds.Overview);
            }

            return;
        }

        SelectGlobalPageByKey(request.GlobalPageId);
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

    private async Task RefreshWorkspaceAsync(ViewModelBase page)
    {
        await _refreshGate.WaitAsync();

        try
        {
            await RefreshLegacyStateAsync();
            await RefreshPageAsync(page);
        }
        finally
        {
            _refreshGate.Release();
        }
    }
}
