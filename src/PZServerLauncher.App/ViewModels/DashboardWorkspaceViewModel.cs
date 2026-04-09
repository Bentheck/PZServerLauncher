using System.Collections.Specialized;
using System.ComponentModel;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.App.ViewModels;

public sealed class DashboardWorkspaceViewModel : WorkspacePageViewModelBase
{
    private ProjectZomboidFleetAccessPostureSummary _fleetAccessPosture = ProjectZomboidFleetAccessPostureSummaryBuilder.Build(Array.Empty<ProjectZomboidProfilePostureSummary>(), remoteAccessEnabled: false);

    public DashboardWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Dashboard",
            "Host status, import discovery, and recent operational activity for the local Project Zomboid environment.",
            "Dashboard is in sync.",
            ["Host summary", "Import candidates", "Recent jobs", "Quick actions"])
    {
        Legacy = legacy;
        Legacy.PropertyChanged += OnLegacyPropertyChanged;
        Legacy.Profiles.CollectionChanged += OnProfilesCollectionChanged;
        Legacy.ImportCandidates.CollectionChanged += OnCollectionChanged;
        Legacy.RecentJobs.CollectionChanged += OnCollectionChanged;

        foreach (var profile in Legacy.Profiles)
        {
            profile.PropertyChanged += OnProfilePropertyChanged;
        }

        RefreshFleetAccessPosture();
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
        : HasProfiles
            ? "Review the fleet posture below, then jump into Profiles or Overview to tune the next server."
            : "Refresh the host, then discover local imports so the panel can surface existing Zomboid servers.";

    public bool HasProfiles => Legacy.Profiles.Count > 0;

    public bool HasNoProfiles => Legacy.Profiles.Count == 0;

    public int ProfileCount => Legacy.Profiles.Count;

    public int InstalledProfileCount => Legacy.Profiles.Count(profile => profile.IsInstallDetected);

    public int ProfilesMissingInstallCount => Legacy.Profiles.Count(profile => !profile.IsInstallDetected);

    public int ModdedProfileCount => Legacy.Profiles.Count(profile => !string.Equals(profile.WorkshopSummary, "0 workshop / 0 mods / 0 maps", StringComparison.Ordinal));

    public int BackupCoverageCount => Legacy.Profiles.Count(profile => profile.HasBackup);

    public int ProfilesMissingBackupCount => Legacy.Profiles.Count(profile => !profile.HasBackup);

    public int DirectJavaReadyProfileCount => Legacy.Profiles.Count(profile => profile.UsesDirectJavaTemplate);

    public int FallbackLaunchProfileCount => Legacy.Profiles.Count(profile => profile.LauncherDetected && !profile.UsesDirectJavaTemplate);

    public int LaunchReadyProfileCount => Legacy.Profiles.Count(profile => profile.IsInstallDetected && profile.ConfigDirectoryDetected && profile.IniDetected && profile.SandboxDetected);

    public string FleetSummary => HasProfiles
        ? $"{InstalledProfileCount}/{Legacy.Profiles.Count} installed | {LaunchReadyProfileCount} launch-ready | {BackupCoverageCount} recovery-ready | {DirectJavaReadyProfileCount} direct-Java | {FallbackLaunchProfileCount} fallback launch | {ModdedProfileCount} modded"
        : "No server fleet posture is available until the first profile exists.";

    public string FleetRiskSummary => !HasProfiles
        ? "No fleet risk posture is available yet."
        : ProfilesMissingBackupCount > 0
            ? $"{ProfilesMissingBackupCount} profile(s) still need a recovery archive before deeper update or mod work."
            : ProfilesMissingInstallCount > 0
                ? $"{ProfilesMissingInstallCount} profile(s) still do not have a detected install footprint."
            : FallbackLaunchProfileCount > 0
                ? $"{FallbackLaunchProfileCount} installed profile(s) are still falling back to the vendor batch launcher."
            : LaunchReadyProfileCount < InstalledProfileCount
                ? $"{InstalledProfileCount - LaunchReadyProfileCount} installed profile(s) still have partial config or cache footprints."
                : "Backups and launch footprints look healthy across the current fleet.";

    public string FleetNextStepSummary => !HasProfiles
        ? "Create or import the first profile to start building a real server fleet."
        : ProfilesMissingBackupCount > 0
            ? "Capture backups for the profiles still missing recovery coverage, then move into config or mod work."
            : ProfilesMissingInstallCount > 0
                ? "Finish installing the remaining profiles so every branch has a real server footprint before deeper tuning."
            : FallbackLaunchProfileCount > 0
                ? "Open Install & Update on the fallback profiles next so the launch path and maintenance posture can be tightened."
            : ModdedProfileCount > 0
                ? "Review the modded profiles next so Workshop, Mods, and Map order still match the local cache."
                : "The fleet looks clean. The next useful move is tuning Sandbox or General settings on the profile you plan to launch next.";

    public string FleetAccessHeadline => _fleetAccessPosture.AccessHeadline;

    public string FleetTrustHeadline => _fleetAccessPosture.TrustHeadline;

    public string FleetCommunicationHeadline => _fleetAccessPosture.CommunicationHeadline;

    public string FleetAccessOperatorSummary => _fleetAccessPosture.OperatorSummary;

    public IReadOnlyList<string> FleetAccessChecklist => _fleetAccessPosture.Checklist;

    public int PublicProfileCount => _fleetAccessPosture.PublicProfileCount;

    public int OpenAccessCount => _fleetAccessPosture.OpenAccessCount;

    public int SafetyEnabledCount => _fleetAccessPosture.SafetyEnabledCount;

    public int VoiceEnabledCount => _fleetAccessPosture.VoiceEnabledCount;

    public int PublicOpenWithoutSafetyCount => _fleetAccessPosture.PublicOpenWithoutSafetyCount;

    public bool HasImportCandidates => Legacy.ImportCandidates.Count > 0;

    public bool HasNoImportCandidates => Legacy.ImportCandidates.Count == 0;

    public bool HasRecentJobs => Legacy.RecentJobs.Count > 0;

    public bool HasNoRecentJobs => Legacy.RecentJobs.Count == 0;

    public int ImportCandidateCount => Legacy.ImportCandidates.Count;

    public int RecentJobCount => Legacy.RecentJobs.Count;

    private void OnProfilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        OnCollectionChanged(sender, e);
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshAll();
    }

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(MainWindowViewModel.HostSummary)
            or nameof(MainWindowViewModel.RemoteSummary)
            or nameof(MainWindowViewModel.OwnerSummary)
            or nameof(MainWindowViewModel.StatusMessage)
            or nameof(MainWindowViewModel.RemoteAccessEnabled))
        {
            RefreshAll();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshFleetAccessPosture();
        OnPropertyChanged(nameof(HostStateSummary));
        OnPropertyChanged(nameof(RemoteAccessSummary));
        OnPropertyChanged(nameof(OwnerSummary));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ImportSummary));
        OnPropertyChanged(nameof(RecentJobSummary));
        OnPropertyChanged(nameof(NextActionSummary));
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
        OnPropertyChanged(nameof(ProfileCount));
        OnPropertyChanged(nameof(InstalledProfileCount));
        OnPropertyChanged(nameof(ProfilesMissingInstallCount));
        OnPropertyChanged(nameof(ModdedProfileCount));
        OnPropertyChanged(nameof(BackupCoverageCount));
        OnPropertyChanged(nameof(ProfilesMissingBackupCount));
        OnPropertyChanged(nameof(DirectJavaReadyProfileCount));
        OnPropertyChanged(nameof(FallbackLaunchProfileCount));
        OnPropertyChanged(nameof(LaunchReadyProfileCount));
        OnPropertyChanged(nameof(FleetSummary));
        OnPropertyChanged(nameof(FleetRiskSummary));
        OnPropertyChanged(nameof(FleetNextStepSummary));
        OnPropertyChanged(nameof(FleetAccessHeadline));
        OnPropertyChanged(nameof(FleetTrustHeadline));
        OnPropertyChanged(nameof(FleetCommunicationHeadline));
        OnPropertyChanged(nameof(FleetAccessOperatorSummary));
        OnPropertyChanged(nameof(FleetAccessChecklist));
        OnPropertyChanged(nameof(PublicProfileCount));
        OnPropertyChanged(nameof(OpenAccessCount));
        OnPropertyChanged(nameof(SafetyEnabledCount));
        OnPropertyChanged(nameof(VoiceEnabledCount));
        OnPropertyChanged(nameof(PublicOpenWithoutSafetyCount));
        OnPropertyChanged(nameof(ImportCandidateCount));
        OnPropertyChanged(nameof(RecentJobCount));
        OnPropertyChanged(nameof(HasImportCandidates));
        OnPropertyChanged(nameof(HasNoImportCandidates));
        OnPropertyChanged(nameof(HasRecentJobs));
        OnPropertyChanged(nameof(HasNoRecentJobs));
    }

    private void RefreshFleetAccessPosture()
    {
        _fleetAccessPosture = ProjectZomboidFleetAccessPostureSummaryBuilder.Build(
            Legacy.Profiles.Select(profile => profile.Posture).ToArray(),
            Legacy.RemoteAccessEnabled);
    }
}
