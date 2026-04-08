using System.Collections.Specialized;

namespace PZServerLauncher.App.ViewModels;

public sealed class DashboardWorkspaceViewModel : WorkspacePageViewModelBase
{
    public DashboardWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Dashboard",
            "Host status, import discovery, and recent operational activity for the local Project Zomboid environment.",
            "Dashboard is in sync.",
            ["Host summary", "Import candidates", "Recent jobs", "Quick actions"])
    {
        Legacy = legacy;
        Legacy.ImportCandidates.CollectionChanged += OnCollectionChanged;
        Legacy.RecentJobs.CollectionChanged += OnCollectionChanged;
    }

    public MainWindowViewModel Legacy { get; }

    public string HostStateSummary => Legacy.HostSummary;

    public string RemoteAccessSummary => Legacy.RemoteSummary;

    public string OwnerSummary => Legacy.OwnerSummary;

    public string StatusSummary => Legacy.StatusMessage;

    public string ImportSummary => HasImportCandidates
        ? $"{ImportCandidateCount} local import candidate(s) discovered."
        : "No import candidates are loaded yet. Run discovery to scan local Zomboid directories.";

    public string RecentJobSummary => HasRecentJobs
        ? $"{RecentJobCount} recent job(s) recorded."
        : "No recent host jobs have been recorded yet.";

    public string NextActionSummary => HasImportCandidates
        ? "Review import candidates, then jump into Profiles to create or import the first server profile."
        : "Refresh the host, then discover local imports so the panel can surface existing Zomboid servers.";

    public bool HasImportCandidates => Legacy.ImportCandidates.Count > 0;

    public bool HasNoImportCandidates => Legacy.ImportCandidates.Count == 0;

    public bool HasRecentJobs => Legacy.RecentJobs.Count > 0;

    public bool HasNoRecentJobs => Legacy.RecentJobs.Count == 0;

    public int ImportCandidateCount => Legacy.ImportCandidates.Count;

    public int RecentJobCount => Legacy.RecentJobs.Count;

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HostStateSummary));
        OnPropertyChanged(nameof(RemoteAccessSummary));
        OnPropertyChanged(nameof(OwnerSummary));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ImportSummary));
        OnPropertyChanged(nameof(RecentJobSummary));
        OnPropertyChanged(nameof(NextActionSummary));
        OnPropertyChanged(nameof(ImportCandidateCount));
        OnPropertyChanged(nameof(RecentJobCount));
        OnPropertyChanged(nameof(HasImportCandidates));
        OnPropertyChanged(nameof(HasNoImportCandidates));
        OnPropertyChanged(nameof(HasRecentJobs));
        OnPropertyChanged(nameof(HasNoRecentJobs));
    }
}
