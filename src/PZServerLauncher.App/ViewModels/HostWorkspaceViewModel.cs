using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public sealed class HostWorkspaceViewModel : WorkspacePageViewModelBase
{
    public HostWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Host",
            "Local host lifecycle controls, startup behavior, and high-level runtime status.",
            "Host settings are in sync.",
            ["Start with Windows", "Stop host", "Stop all + host", "Exit desktop"])
    {
        Legacy = legacy;
        Legacy.PropertyChanged += OnLegacyPropertyChanged;
        Legacy.Profiles.CollectionChanged += OnProfilesCollectionChanged;
        foreach (var profile in Legacy.Profiles)
        {
            profile.PropertyChanged += OnProfilePropertyChanged;
        }
    }

    public MainWindowViewModel Legacy { get; }

    public string HostStatusSummary => Legacy.HostSummary;

    public string HostLifecycleSummary => CurrentSummary.LifecycleHeadline;

    public string HostStartupSummary => Legacy.HostStartWithWindows
        ? "Windows will start the host automatically at sign-in."
        : "Windows startup is disabled until you opt in.";

    public string HostStartupLabel => Legacy.HostStartWithWindows
        ? "Windows startup on"
        : "Windows startup off";

    public string HostFleetSummary => CurrentSummary.FleetHeadline;

    public int ManagedProfileCount => Legacy.Profiles.Count;

    public int StartupRosterCount => Legacy.Profiles.Count(profile => profile.EditableStartWithHost);

    public int AutoRestartCoverageCount => Legacy.Profiles.Count(profile => profile.EditableAutoRestartOnCrash);

    public int BackupCoverageCount => Legacy.Profiles.Count(profile => profile.HasBackup);

    public int InstalledProfileCount => Legacy.Profiles.Count(profile => profile.IsInstallDetected);

    public int RunningProfileCount => Legacy.Profiles.Count(profile => string.Equals(profile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase));

    public string HostStartupFleetSummary => Legacy.Profiles.Count == 0
        ? "No startup roster yet."
        : $"{StartupRosterCount} profile(s) start with host | {AutoRestartCoverageCount} auto-restart on crash | {BackupCoverageCount} with recovery coverage.";

    public string HostStartupCoverageSummary => CurrentSummary.StartupHeadline;

    public string HostRuntimeCoverageSummary => CurrentSummary.RuntimeHeadline;

    public string HostExposureSummary => CurrentSummary.ExposureHeadline;

    public string HostSecuritySummary => CurrentSummary.SecurityHeadline;

    public string HostRecoverySummary => CurrentSummary.RecoveryHeadline;

    public string HostRecoveryCoverageSummary => CurrentSummary.RiskHeadline;

    public string HostAutomationSummary => CurrentSummary.AutomationHeadline;

    public string HostShutdownSummary => "Stop Host ends only the orchestration process. Stop All + Host shuts down managed servers first, then closes the host.";

    public string HostOperatorSummary => CurrentSummary.OperatorSummary;

    public string HostActionSummary => "Use Save to persist startup behavior, keep remote access loopback-only until HTTPS is validated, and stop the host explicitly when you want orchestration to end.";

    public string HostRiskSummary => CurrentSummary.RiskHeadline;

    public string HostNextStepSummary => CurrentSummary.NextStepSummary;

    public IReadOnlyList<ProjectZomboidOperatorChecklistItem> HostChecklist => CurrentSummary.Checklist;

    public IReadOnlyList<HostManagedProfileRowViewModel> ManagedProfiles => Legacy.Profiles
        .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
        .Select(profile => new HostManagedProfileRowViewModel(
            profile.DisplayName,
            profile.Branch,
            profile.RuntimeState,
            profile.EditableStartWithHost,
            profile.EditableAutoRestartOnCrash,
            profile.HasBackup,
            profile.IsInstallDetected,
            profile.Ports,
            BuildRosterSummary(profile)))
        .ToArray();

    public bool HasManagedProfiles => ManagedProfiles.Count > 0;

    public bool HasNoManagedProfiles => !HasManagedProfiles;

    private ProjectZomboidHostOperatorSummary CurrentSummary =>
        ProjectZomboidHostOperatorSummaryBuilder.Build(BuildHostSettingsSnapshot(), BuildManagedProfileSnapshots());

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.HostSummary) ||
            e.PropertyName == nameof(MainWindowViewModel.StatusMessage) ||
            e.PropertyName == nameof(MainWindowViewModel.HostStartWithWindows) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteAccessEnabled) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteBindAddress) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteHttpsPort) ||
            e.PropertyName == nameof(MainWindowViewModel.OwnerSummary) ||
            e.PropertyName == nameof(MainWindowViewModel.OwnerBootstrapRequired))
        {
            RefreshSummaryProperties();
        }
    }

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

        RefreshSummaryProperties();
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshSummaryProperties();

    private void RefreshSummaryProperties()
    {
        OnPropertyChanged(nameof(HostStatusSummary));
        OnPropertyChanged(nameof(HostLifecycleSummary));
        OnPropertyChanged(nameof(HostStartupSummary));
        OnPropertyChanged(nameof(HostStartupLabel));
        OnPropertyChanged(nameof(HostFleetSummary));
        OnPropertyChanged(nameof(ManagedProfileCount));
        OnPropertyChanged(nameof(StartupRosterCount));
        OnPropertyChanged(nameof(AutoRestartCoverageCount));
        OnPropertyChanged(nameof(BackupCoverageCount));
        OnPropertyChanged(nameof(InstalledProfileCount));
        OnPropertyChanged(nameof(RunningProfileCount));
        OnPropertyChanged(nameof(HostStartupFleetSummary));
        OnPropertyChanged(nameof(HostStartupCoverageSummary));
        OnPropertyChanged(nameof(HostRuntimeCoverageSummary));
        OnPropertyChanged(nameof(HostExposureSummary));
        OnPropertyChanged(nameof(HostSecuritySummary));
        OnPropertyChanged(nameof(HostRecoverySummary));
        OnPropertyChanged(nameof(HostRecoveryCoverageSummary));
        OnPropertyChanged(nameof(HostAutomationSummary));
        OnPropertyChanged(nameof(HostShutdownSummary));
        OnPropertyChanged(nameof(HostOperatorSummary));
        OnPropertyChanged(nameof(HostActionSummary));
        OnPropertyChanged(nameof(HostRiskSummary));
        OnPropertyChanged(nameof(HostNextStepSummary));
        OnPropertyChanged(nameof(HostChecklist));
        OnPropertyChanged(nameof(ManagedProfiles));
        OnPropertyChanged(nameof(HasManagedProfiles));
        OnPropertyChanged(nameof(HasNoManagedProfiles));
    }

    private HostSettings BuildHostSettingsSnapshot() =>
        new()
        {
            StartHostWithWindows = Legacy.HostStartWithWindows,
            RemoteAccess = new RemoteAccessSettings
            {
                IsEnabled = Legacy.RemoteAccessEnabled,
                BindAddress = Legacy.RemoteBindAddress,
                HttpsPort = int.TryParse(Legacy.RemoteHttpsPort, out var parsedPort) ? parsedPort : 8443,
            },
            OwnerBootstrap = new OwnerBootstrapState(
                !Legacy.OwnerBootstrapRequired,
                OwnerUserId: null,
                OwnerUserName: Legacy.OwnerBootstrapRequired ? null : "Owner",
                ConfiguredAtUtc: null),
        };

    private ProjectZomboidHostManagedProfileSnapshot[] BuildManagedProfileSnapshots() =>
        Legacy.Profiles
            .Select(profile => new ProjectZomboidHostManagedProfileSnapshot(
                profile.DisplayName,
                profile.Branch,
                profile.RuntimeState,
                profile.EditableStartWithHost,
                profile.EditableAutoRestartOnCrash,
                profile.HasBackup,
                profile.IsInstallDetected,
                profile.Ports))
            .ToArray();

    private static string BuildRosterSummary(ProfileCardViewModel profile)
    {
        var posture = new List<string>
        {
            profile.EditableStartWithHost ? "Starts with host" : "Manual start",
            profile.EditableAutoRestartOnCrash ? "Auto-restart on crash" : "Manual crash recovery",
            profile.HasBackup ? "Recovery archive present" : "Needs first backup",
            profile.IsInstallDetected ? "Install detected" : "Install missing",
        };

        return string.Join(" | ", posture);
    }

    public sealed record HostManagedProfileRowViewModel(
        string DisplayName,
        string Branch,
        string RuntimeState,
        bool StartWithHost,
        bool AutoRestartOnCrash,
        bool HasBackup,
        bool InstallDetected,
        string Ports,
        string RosterSummary);
}
