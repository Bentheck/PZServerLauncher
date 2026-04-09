using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

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

    public string HostLifecycleSummary => Legacy.StatusMessage;

    public string HostStartupSummary => Legacy.HostStartWithWindows
        ? "Windows will start the host automatically at sign-in."
        : "Windows startup is disabled until you opt in.";

    public string HostStartupLabel => Legacy.HostStartWithWindows
        ? "Start with Windows: On"
        : "Start with Windows: Off";

    public string HostFleetSummary => Legacy.Profiles.Count == 0
        ? "No Project Zomboid server profiles are loaded yet."
        : $"{Legacy.Profiles.Count} profile(s) loaded | {Legacy.Profiles.Count(profile => profile.IsInstallDetected)} installed | {Legacy.Profiles.Count(profile => string.Equals(profile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase))} running.";

    public int ManagedProfileCount => Legacy.Profiles.Count;

    public int StartupRosterCount => Legacy.Profiles.Count(profile => profile.EditableStartWithHost);

    public int AutoRestartCoverageCount => Legacy.Profiles.Count(profile => profile.EditableAutoRestartOnCrash);

    public int BackupCoverageCount => Legacy.Profiles.Count(profile => profile.HasBackup);

    public int InstalledProfileCount => Legacy.Profiles.Count(profile => profile.IsInstallDetected);

    public int RunningProfileCount => Legacy.Profiles.Count(profile => string.Equals(profile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase));

    public string HostStartupFleetSummary => Legacy.Profiles.Count == 0
        ? "No startup roster yet."
        : $"{Legacy.Profiles.Count(profile => profile.EditableStartWithHost)} profile(s) start with host | {Legacy.Profiles.Count(profile => profile.EditableAutoRestartOnCrash)} auto-restart on crash | {Legacy.Profiles.Count(profile => profile.HasBackup)} with recovery coverage.";

    public string HostStartupCoverageSummary => Legacy.Profiles.Count == 0
        ? "Startup posture appears after the first profile is created or imported."
        : StartupRosterCount == 0
            ? "No profiles currently start with the host, so this machine still behaves like a manual launcher."
            : $"{StartupRosterCount} profile(s) are staged to come online with the host.";

    public string HostRuntimeCoverageSummary => Legacy.Profiles.Count == 0
        ? "No runtime roster yet."
        : RunningProfileCount == 0
            ? "Nothing is running right now. Use Overview or Install & Update to bring a server online."
            : $"{RunningProfileCount} profile(s) are currently online under this host.";

    public string HostExposureSummary => Legacy.RemoteAccessEnabled
        ? $"Remote HTTPS is staged for {Legacy.RemoteBindAddress}:{Legacy.RemoteHttpsPort} once the host is restarted."
        : "The host is loopback-only right now. Remote access stays off until you enable and validate HTTPS.";

    public string HostSecuritySummary => Legacy.OwnerSummary;

    public string HostRecoverySummary => Legacy.Profiles.Count == 0
        ? "Recovery posture will appear after you create or import the first server."
        : $"{Legacy.Profiles.Count(profile => profile.HasBackup)} profile(s) already have at least one recovery archive.";

    public string HostRecoveryCoverageSummary => Legacy.Profiles.Count == 0
        ? "No recovery roster yet."
        : BackupCoverageCount == ManagedProfileCount
            ? "Every loaded profile already has at least one recovery archive."
            : $"{ManagedProfileCount - BackupCoverageCount} profile(s) still need their first backup archive.";

    public string HostAutomationSummary => Legacy.Profiles.Count == 0
        ? "Automation posture appears after the first profile exists."
        : AutoRestartCoverageCount == 0
            ? "Auto-restart is off across the fleet, so crashes will stay down until an operator intervenes."
            : $"{AutoRestartCoverageCount} profile(s) auto-restart after a crash.";

    public string HostShutdownSummary => "Stop Host ends only the orchestration process. Stop All + Host shuts down managed servers first, then closes the host.";

    public string HostOperatorSummary => Legacy.HostStartWithWindows
        ? "Recommended for always-on operators who want the host ready immediately after login."
        : "Enable startup when you want this machine to behave like a persistent server controller.";

    public string HostActionSummary => "Use Save to persist startup behavior, keep remote access loopback-only until HTTPS is validated, and stop the host explicitly when you want orchestration to end.";

    public string HostRiskSummary
    {
        get
        {
            if (Legacy.Profiles.Count == 0)
            {
                return "No fleet risk is visible yet because the host is not supervising any profiles.";
            }

            if (StartupRosterCount > 0 && InstalledProfileCount < StartupRosterCount)
            {
                return "At least one profile is marked to start with host but does not currently show an install.";
            }

            if (BackupCoverageCount < ManagedProfileCount)
            {
                return "Some profiles still lack recovery coverage. Take the first backup before this machine becomes always-on.";
            }

            if (Legacy.RemoteAccessEnabled && !Legacy.OwnerBootstrapRequired)
            {
                return "Remote access is staged, so review Users and 2FA posture before you expose the host outside the desktop.";
            }

            return "The host posture is steady: startup, recovery, and exposure look coherent for the current fleet.";
        }
    }

    public string HostNextStepSummary => Legacy.Profiles.Count == 0
        ? "Import or create the first profile, then decide whether this machine should behave like an always-on controller."
        : Legacy.RemoteAccessEnabled
            ? "Review Remote Access and Users next so the optional web surface is secure before you expose it."
            : "Decide whether this machine should stay desktop-only or whether you want to prepare the optional remote web surface.";

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

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.HostSummary) ||
            e.PropertyName == nameof(MainWindowViewModel.StatusMessage) ||
            e.PropertyName == nameof(MainWindowViewModel.HostStartWithWindows) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteAccessEnabled) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteBindAddress) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteHttpsPort) ||
            e.PropertyName == nameof(MainWindowViewModel.OwnerSummary))
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
            OnPropertyChanged(nameof(HostOperatorSummary));
            OnPropertyChanged(nameof(HostActionSummary));
            OnPropertyChanged(nameof(HostRiskSummary));
            OnPropertyChanged(nameof(HostNextStepSummary));
            OnPropertyChanged(nameof(ManagedProfiles));
            OnPropertyChanged(nameof(HasManagedProfiles));
            OnPropertyChanged(nameof(HasNoManagedProfiles));
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
        OnPropertyChanged(nameof(HostRecoverySummary));
        OnPropertyChanged(nameof(HostRecoveryCoverageSummary));
        OnPropertyChanged(nameof(HostAutomationSummary));
        OnPropertyChanged(nameof(HostRiskSummary));
        OnPropertyChanged(nameof(HostNextStepSummary));
        OnPropertyChanged(nameof(ManagedProfiles));
        OnPropertyChanged(nameof(HasManagedProfiles));
        OnPropertyChanged(nameof(HasNoManagedProfiles));
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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
        OnPropertyChanged(nameof(HostRecoverySummary));
        OnPropertyChanged(nameof(HostRecoveryCoverageSummary));
        OnPropertyChanged(nameof(HostAutomationSummary));
        OnPropertyChanged(nameof(HostRiskSummary));
        OnPropertyChanged(nameof(ManagedProfiles));
        OnPropertyChanged(nameof(HasManagedProfiles));
        OnPropertyChanged(nameof(HasNoManagedProfiles));
    }

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
