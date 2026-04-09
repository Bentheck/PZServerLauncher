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

    public WorkspaceShellViewModel()
        : this(
            new MainWindowViewModel(),
            new LocalHostApiClient(),
            new RuntimeEventStream(new LocalHostApiClient()),
            new DesktopShellService(),
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
        Host = new HostWorkspaceViewModel(legacy);
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

    public string WorkspaceGuidance => CurrentPage switch
    {
        ProfilesWorkspaceViewModel => "Pick a server profile, then move through install, sandbox, mods, backups, logs, and advanced files without leaving the workspace.",
        DashboardWorkspaceViewModel => "Use the dashboard as your local control room for imports, host posture, and recent activity.",
        HostWorkspaceViewModel => "Host controls stay global so you can manage startup, runtime, and process posture without opening a specific server.",
        RemoteAccessWorkspaceViewModel => "Remote access stays optional. Configure HTTPS exposure only when you are ready to operate the server from another device.",
        UsersWorkspaceViewModel => "Use Users for owner bootstrap, operator creation, and role management when web administration is enabled.",
        _ => CurrentPageSummary,
    };

    public string ProfileCountSummary => Legacy.Profiles.Count == 0
        ? "No profiles loaded yet."
        : $"{Legacy.Profiles.Count} profile{(Legacy.Profiles.Count == 1 ? string.Empty : "s")} loaded in the launcher.";

    public string RecentActivitySummary => Legacy.RecentJobs.Count == 0
        ? "No recent jobs have been recorded yet."
        : $"{Legacy.RecentJobs.Count} recent host job{(Legacy.RecentJobs.Count == 1 ? string.Empty : "s")} available for quick review.";

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
}
