using System.ComponentModel;
using System.Linq;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public sealed class RemoteAccessWorkspaceViewModel : WorkspacePageViewModelBase
{
    public RemoteAccessWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Remote Access",
            "Optional HTTPS rollout console, local validation, and Windows Firewall rule control for the web workspace.",
            "Remote access rollout is in sync.",
            ["HTTPS binding", "Local self-test", "Firewall rule", "Desktop-driven setup"])
    {
        Legacy = legacy;
        Legacy.PropertyChanged += OnLegacyPropertyChanged;
    }

    public MainWindowViewModel Legacy { get; }

    public bool RemoteAccessIsEnabled => Legacy.RemoteAccessEnabled;

    public bool RemoteAccessIsDisabled => !RemoteAccessIsEnabled;

    public string RemoteModeSummary => CurrentSummary.ModeHeadline;

    public string RemotePostureSummary => CurrentSummary.OperatorSummary;

    public string RemoteStatusSummary => Legacy.RemoteWizardStatus;

    public string RemoteBindingSummary => CurrentSummary.EndpointHeadline;

    public string RemoteEndpointSummary => CurrentSummary.EndpointHeadline;

    public string RemoteExposureSummary => CurrentSummary.ModeHeadline;

    public string RemoteCertificateSummary => CurrentSummary.CertificateHeadline;

    public string RemoteCertificateStatusSummary => CurrentSummary.CertificateHeadline;

    public string RemoteFirewallSummary => CurrentSummary.FirewallHeadline;

    public string RemoteFirewallActionSummary => CurrentSummary.OperatorSummary;

    public string RemoteHostSummary => string.IsNullOrWhiteSpace(Legacy.RemotePublicHostname)
        ? "No public hostname is set. The bind address will be used for local exposure."
        : $"Public hostname: {Legacy.RemotePublicHostname}";

    public string RemoteIdentitySummary => Legacy.OwnerSummary;

    public string RemoteActionSummary => CurrentSummary.OperatorSummary;

    public string RemoteReadinessSummary => CurrentSummary.ReadinessHeadline;

    public string RemoteEnabledLabel => Legacy.RemoteAccessEnabled
        ? "Enabled: On"
        : "Enabled: Off";

    public string RemoteSelfTestHeadline => !HasRemoteSelfTestChecks
        ? "No self-test has been run yet."
        : RemoteBlockingCheckCount > 0
            ? "The latest rollout posture still has blocking items."
            : "The latest rollout posture looks actionable.";

    public string RemoteSelfTestStats => !HasRemoteSelfTestChecks
        ? "No local checks captured yet."
        : $"{RemoteSelfTestCheckCount} check(s) recorded | {RemoteBlockingCheckCount} blocking signal(s) | {RemoteFollowUpCheckCount} follow-up reminder(s).";

    public string RemoteFirewallIntentSummary => CurrentSummary.FirewallHeadline;

    public string RemoteOperatorChecklistSummary => CurrentSummary.OperatorSummary;

    public string RemoteLocalOnlyGuidance => Legacy.RemoteAccessEnabled
        ? "The machine is no longer loopback-only. Keep exposure limited until the certificate, endpoint, and firewall posture all look healthy."
        : "Nothing leaves the machine yet, which is the safest state for desktop-first administration.";

    public IReadOnlyList<ProjectZomboidOperatorChecklistItem> RemoteSelfTestChecks => CurrentSummary.Checklist;

    public int RemoteSelfTestCheckCount => RemoteSelfTestChecks.Count;

    public int RemoteBlockingCheckCount => RemoteSelfTestChecks.Count(check => check.IsBlocking);

    public int RemoteFollowUpCheckCount => RemoteSelfTestChecks.Count(check => check.IsFollowUp);

    public int RemoteHealthyCheckCount => RemoteSelfTestChecks.Count(check => !check.IsBlocking && !check.IsFollowUp);

    public bool HasRemoteSelfTestChecks => RemoteSelfTestChecks.Count > 0;

    public bool HasNoRemoteSelfTestChecks => !HasRemoteSelfTestChecks;

    public string RemoteChecklistHeadline => RemoteSelfTestHeadline;

    public string RemoteChecklistSummary => !HasRemoteSelfTestChecks
        ? "Run the local self-test after saving remote settings to turn this panel into a rollout checklist."
        : $"{RemoteSelfTestCheckCount} check(s) recorded | {RemoteBlockingCheckCount} blocking | {RemoteFollowUpCheckCount} follow-up | {RemoteHealthyCheckCount} healthy.";

    public string RemoteNextStepSummary => CurrentSummary.NextStepSummary;

    private ProjectZomboidRemoteAccessRolloutSummary CurrentSummary =>
        ProjectZomboidRemoteAccessRolloutSummaryBuilder.Build(
            BuildRemoteAccessSettingsSnapshot(),
            BuildEstimatedOwnerTwoFactorCount(),
            GetSelfTestLines());

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteAccessEnabled) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteBindAddress) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteHttpsPort) ||
            e.PropertyName == nameof(MainWindowViewModel.RemotePublicHostname) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteCertificatePath) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteCreateFirewallRule) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteWizardStatus) ||
            e.PropertyName == nameof(MainWindowViewModel.RemoteSelfTestChecks) ||
            e.PropertyName == nameof(MainWindowViewModel.OwnerBootstrapRequired) ||
            e.PropertyName == nameof(MainWindowViewModel.OwnerSummary))
        {
            RefreshSummaryProperties();
        }
    }

    private void RefreshSummaryProperties()
    {
        OnPropertyChanged(nameof(RemoteAccessIsEnabled));
        OnPropertyChanged(nameof(RemoteAccessIsDisabled));
        OnPropertyChanged(nameof(RemoteModeSummary));
        OnPropertyChanged(nameof(RemotePostureSummary));
        OnPropertyChanged(nameof(RemoteStatusSummary));
        OnPropertyChanged(nameof(RemoteBindingSummary));
        OnPropertyChanged(nameof(RemoteEndpointSummary));
        OnPropertyChanged(nameof(RemoteExposureSummary));
        OnPropertyChanged(nameof(RemoteCertificateSummary));
        OnPropertyChanged(nameof(RemoteCertificateStatusSummary));
        OnPropertyChanged(nameof(RemoteFirewallSummary));
        OnPropertyChanged(nameof(RemoteFirewallActionSummary));
        OnPropertyChanged(nameof(RemoteFirewallIntentSummary));
        OnPropertyChanged(nameof(RemoteHostSummary));
        OnPropertyChanged(nameof(RemoteIdentitySummary));
        OnPropertyChanged(nameof(RemoteActionSummary));
        OnPropertyChanged(nameof(RemoteReadinessSummary));
        OnPropertyChanged(nameof(RemoteEnabledLabel));
        OnPropertyChanged(nameof(RemoteSelfTestChecks));
        OnPropertyChanged(nameof(HasRemoteSelfTestChecks));
        OnPropertyChanged(nameof(HasNoRemoteSelfTestChecks));
        OnPropertyChanged(nameof(RemoteSelfTestHeadline));
        OnPropertyChanged(nameof(RemoteSelfTestStats));
        OnPropertyChanged(nameof(RemoteOperatorChecklistSummary));
        OnPropertyChanged(nameof(RemoteLocalOnlyGuidance));
        OnPropertyChanged(nameof(RemoteSelfTestCheckCount));
        OnPropertyChanged(nameof(RemoteBlockingCheckCount));
        OnPropertyChanged(nameof(RemoteFollowUpCheckCount));
        OnPropertyChanged(nameof(RemoteHealthyCheckCount));
        OnPropertyChanged(nameof(RemoteChecklistHeadline));
        OnPropertyChanged(nameof(RemoteChecklistSummary));
        OnPropertyChanged(nameof(RemoteNextStepSummary));
    }

    private RemoteAccessSettings BuildRemoteAccessSettingsSnapshot() =>
        new()
        {
            IsEnabled = Legacy.RemoteAccessEnabled,
            BindAddress = Legacy.RemoteBindAddress,
            HttpsPort = int.TryParse(Legacy.RemoteHttpsPort, out var parsedPort) ? parsedPort : 8443,
            PublicHostname = string.IsNullOrWhiteSpace(Legacy.RemotePublicHostname) ? null : Legacy.RemotePublicHostname,
            CertificatePath = string.IsNullOrWhiteSpace(Legacy.RemoteCertificatePath) ? null : Legacy.RemoteCertificatePath,
            CreateFirewallRule = Legacy.RemoteCreateFirewallRule,
            RequiresHostRestart = Legacy.RemoteAccessEnabled,
        };

    private int BuildEstimatedOwnerTwoFactorCount()
    {
        if (Legacy.OwnerBootstrapRequired)
        {
            return 0;
        }

        var lines = GetSelfTestLines();
        var hasTwoFactorBlocker = lines.Any(line =>
            (line.Contains("two-factor", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("totp", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("authenticator", StringComparison.OrdinalIgnoreCase)) &&
            (line.Contains("required", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("failed", StringComparison.OrdinalIgnoreCase)));

        return hasTwoFactorBlocker ? 0 : 1;
    }

    private string[] GetSelfTestLines() =>
        string.IsNullOrWhiteSpace(Legacy.RemoteSelfTestChecks)
            ? []
            : Legacy.RemoteSelfTestChecks
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
