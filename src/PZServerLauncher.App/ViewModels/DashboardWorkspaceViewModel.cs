using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.App.ViewModels;

public sealed class DashboardWorkspaceViewModel : WorkspacePageViewModelBase
{
    private ProjectZomboidFleetAccessPostureSummary _fleetAccessPosture = ProjectZomboidFleetAccessPostureSummaryBuilder.Build(Array.Empty<ProjectZomboidProfilePostureSummary>(), remoteAccessEnabled: false);

    public DashboardWorkspaceViewModel(
        MainWindowViewModel legacy,
        Action openProfilesWorkspace,
        Action openUsersWorkspace)
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

        OpenProfilesWorkspaceCommand = new RelayCommand(openProfilesWorkspace);
        OpenUsersWorkspaceCommand = new RelayCommand(openUsersWorkspace);
        RefreshFleetAccessPosture();
    }

    public MainWindowViewModel Legacy { get; }

    public IRelayCommand OpenProfilesWorkspaceCommand { get; }

    public IRelayCommand OpenUsersWorkspaceCommand { get; }

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

    public string SetupModeHeadline => OwnerBootstrapPending
        ? "Finish the owner account, then bring the first server under management."
        : HasImportCandidates
            ? "Adopt an existing local server or start a new managed one."
            : "Start with a new managed server or adopt a local one.";

    public string SetupModeSummary => OwnerBootstrapPending
        ? "Desktop control is already available, but the optional web admin surface should not be treated as production-ready until an owner account exists."
        : HasImportCandidates
            ? "A local Project Zomboid footprint is already on this machine. Import it if you want to keep existing files, or create a clean managed server instead."
            : "No managed servers exist yet. Create a starter profile for a clean setup, or scan the machine to adopt an existing local host.";

    public string SetupPrimaryActionLabel => "New Managed Server";

    public string SetupSecondaryActionLabel => HasImportCandidates ? "Scan Again" : "Scan Local Servers";

    public bool ShowOwnerSetupAction => OwnerBootstrapPending;

    public bool ShowFleetMode => HasProfiles;

    public bool OwnerBootstrapPending => Legacy.OwnerBootstrapRequired;

    public bool HasGuidedLaunchPad => OwnerBootstrapPending || HasNoProfiles || HasImportCandidates;

    public bool IsFirstRun => !HasProfiles && !HasImportCandidates;

    public string LaunchPadHeadline => OwnerBootstrapPending
        ? "Desktop control is ready, but privileged remote access still needs an owner account."
        : HasImportCandidates
            ? $"{ImportCandidateCount} local server candidate(s) are ready for intake."
            : HasProfiles
                ? "Your fleet is online. Use the board below to decide the next server action."
                : "Create or import the first managed server.";

    public string LaunchPadSummary => OwnerBootstrapPending
        ? "You can keep using the desktop now, but finish owner bootstrap from Users before you treat the optional web surface as production-ready."
        : HasProfiles
            ? "This panel is now a true fleet board. Use it to decide what to launch, repair, or tighten next."
            : HasImportCandidates
                ? "Discovery already found local Zomboid footprints. Import one of them, verify install posture, then capture a first backup."
                : "Start with Create Profile for a clean managed server, or scan the local machine if you already have a Zomboid host to adopt.";

    public string FirstRunActionPlan => OwnerBootstrapPending
        ? "Bootstrap the owner account, bring the first server under management, then verify install and recovery posture before considering remote exposure."
        : HasImportCandidates
            ? "Bring one local server under management first, then verify install, backup, and launch posture before the first live boot."
            : "The fastest path is create or import, install the server footprint, capture a first backup, then tune settings before launch.";

    public string LaunchPadStepOne => OwnerBootstrapPending
        ? "Step 1: Open Users and create the initial owner account."
        : HasImportCandidates
            ? "Step 1: Review the local server candidates and import the one you want to manage first."
            : "Step 1: Create a starter profile or scan the local Zomboid directories for an existing server.";

    public string LaunchPadStepTwo => OwnerBootstrapPending
        ? HasImportCandidates
            ? "Step 2: Import the first discovered server so the managed roster starts with a real footprint."
            : "Step 2: Create or import the first managed server profile."
        : HasImportCandidates
            ? "Step 2: Open Profiles and confirm install, cache, and recovery posture for the imported server."
            : "Step 2: Use Install & Update to create the dedicated server footprint and verify branch isolation.";

    public string LaunchPadStepThree => OwnerBootstrapPending
        ? "Step 3: Once the first profile exists, verify install/update posture and take a first backup."
        : HasImportCandidates
            ? "Step 3: Tune General, Sandbox, Mods & Maps, and Network before the first live launch."
            : "Step 3: Tune the structured settings, capture a first backup, then launch from Overview.";

    public string LaunchPadActionHint => OwnerBootstrapPending
        ? "Users is the next stop if you plan to expose remote admin. Profiles is the next stop if you want to stay desktop-only and get the first server online."
        : HasImportCandidates
            ? "Import first if you already have a local footprint. Create first if you want a clean managed server."
            : HasProfiles
                ? "Move into Profiles to pick the next active server."
                : "Create Profile starts from scratch. Scan Imports looks for local Zomboid footprints you can adopt.";

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

    public ImportCandidateViewModel? FirstImportCandidate => Legacy.ImportCandidates.FirstOrDefault();

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
        OnPropertyChanged(nameof(SetupModeHeadline));
        OnPropertyChanged(nameof(SetupModeSummary));
        OnPropertyChanged(nameof(SetupPrimaryActionLabel));
        OnPropertyChanged(nameof(SetupSecondaryActionLabel));
        OnPropertyChanged(nameof(ShowOwnerSetupAction));
        OnPropertyChanged(nameof(ShowFleetMode));
        OnPropertyChanged(nameof(OwnerBootstrapPending));
        OnPropertyChanged(nameof(HasGuidedLaunchPad));
        OnPropertyChanged(nameof(IsFirstRun));
        OnPropertyChanged(nameof(LaunchPadHeadline));
        OnPropertyChanged(nameof(LaunchPadSummary));
        OnPropertyChanged(nameof(FirstRunActionPlan));
        OnPropertyChanged(nameof(LaunchPadStepOne));
        OnPropertyChanged(nameof(LaunchPadStepTwo));
        OnPropertyChanged(nameof(LaunchPadStepThree));
        OnPropertyChanged(nameof(LaunchPadActionHint));
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
        OnPropertyChanged(nameof(FirstImportCandidate));
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
