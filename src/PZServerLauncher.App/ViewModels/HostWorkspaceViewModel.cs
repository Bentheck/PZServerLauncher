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
        : $"{Legacy.Profiles.Count} profile(s) loaded | {Legacy.Profiles.Count(profile => profile.IsInstallDetected)} installed | {Legacy.Profiles.Count(profile => string.Equals(profile.RuntimeState, \"Running\", StringComparison.OrdinalIgnoreCase))} running.";

    public string HostStartupFleetSummary => Legacy.Profiles.Count == 0
        ? "No startup roster yet."
        : $"{Legacy.Profiles.Count(profile => profile.EditableStartWithHost)} profile(s) start with host | {Legacy.Profiles.Count(profile => profile.EditableAutoRestartOnCrash)} auto-restart on crash | {Legacy.Profiles.Count(profile => profile.HasBackup)} with recovery coverage.";

    public string HostExposureSummary => Legacy.RemoteAccessEnabled
        ? $"Remote HTTPS is staged for {Legacy.RemoteBindAddress}:{Legacy.RemoteHttpsPort} once the host is restarted."
        : "The host is loopback-only right now. Remote access stays off until you enable and validate HTTPS.";

    public string HostSecuritySummary => Legacy.OwnerSummary;

    public string HostRecoverySummary => Legacy.Profiles.Count == 0
        ? "Recovery posture will appear after you create or import the first server."
        : $"{Legacy.Profiles.Count(profile => profile.HasBackup)} profile(s) already have at least one recovery archive.";

    public string HostShutdownSummary => "Stop Host ends only the orchestration process. Stop All + Host shuts down managed servers first, then closes the host.";

    public string HostOperatorSummary => Legacy.HostStartWithWindows
        ? "Recommended for always-on operators who want the host ready immediately after login."
        : "Enable startup when you want this machine to behave like a persistent server controller.";

    public string HostActionSummary => "Use Save to persist startup behavior, keep remote access loopback-only until HTTPS is validated, and stop the host explicitly when you want orchestration to end.";

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
            profile.Ports))
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
            OnPropertyChanged(nameof(HostStartupFleetSummary));
            OnPropertyChanged(nameof(HostExposureSummary));
            OnPropertyChanged(nameof(HostSecuritySummary));
            OnPropertyChanged(nameof(HostRecoverySummary));
            OnPropertyChanged(nameof(HostOperatorSummary));
            OnPropertyChanged(nameof(HostActionSummary));
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
        OnPropertyChanged(nameof(HostStartupFleetSummary));
        OnPropertyChanged(nameof(HostRecoverySummary));
        OnPropertyChanged(nameof(HostNextStepSummary));
        OnPropertyChanged(nameof(ManagedProfiles));
        OnPropertyChanged(nameof(HasManagedProfiles));
        OnPropertyChanged(nameof(HasNoManagedProfiles));
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HostFleetSummary));
        OnPropertyChanged(nameof(HostStartupFleetSummary));
        OnPropertyChanged(nameof(HostRecoverySummary));
        OnPropertyChanged(nameof(ManagedProfiles));
        OnPropertyChanged(nameof(HasManagedProfiles));
        OnPropertyChanged(nameof(HasNoManagedProfiles));
    }

    public sealed record HostManagedProfileRowViewModel(
        string DisplayName,
        string Branch,
        string RuntimeState,
        bool StartWithHost,
        bool AutoRestartOnCrash,
        bool HasBackup,
        bool InstallDetected,
        string Ports);
}
