using System.ComponentModel;
using System.Linq;

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

    public string RemoteModeSummary => Legacy.RemoteAccessEnabled
        ? "Remote access is staged for HTTPS exposure."
        : "The host stays loopback-only until you explicitly enable HTTPS.";

    public string RemotePostureSummary => Legacy.RemoteAccessEnabled
        ? "This machine is prepared to expose a web admin endpoint once the certificate, bind address, and firewall posture all line up."
        : "This machine remains local-first. Nothing is externally exposed yet.";

    public string RemoteStatusSummary => Legacy.RemoteWizardStatus;

    public string RemoteBindingSummary => Legacy.RemoteAccessEnabled
        ? $"HTTPS will bind to {Legacy.RemoteBindAddress}:{Legacy.RemoteHttpsPort}."
        : "Loopback-only mode is active, so the host is not advertising a remote listener.";

    public string RemoteEndpointSummary => Legacy.RemoteAccessEnabled
        ? $"Planned endpoint: https://{(string.IsNullOrWhiteSpace(Legacy.RemotePublicHostname) ? Legacy.RemoteBindAddress : Legacy.RemotePublicHostname)}:{Legacy.RemoteHttpsPort}"
        : "No remote endpoint is currently exposed.";

    public string RemoteExposureSummary => Legacy.RemoteAccessEnabled
        ? "Exposure is intentionally opt-in. Review the endpoint, certificate, and firewall posture before opening this surface beyond the local machine."
        : "Exposure is intentionally off. You can finish the local admin workflow without enabling the remote listener.";

    public string RemoteCertificateSummary => string.IsNullOrWhiteSpace(Legacy.RemoteCertificatePath)
        ? "No certificate path has been selected yet."
        : $"Certificate path: {Legacy.RemoteCertificatePath}";

    public string RemoteCertificateStatusSummary => string.IsNullOrWhiteSpace(Legacy.RemoteCertificatePath)
        ? "Choose a PFX path before you enable HTTPS so the host can validate the listener."
        : Legacy.RemoteAccessEnabled
            ? "Certificate input is present and ready for host validation."
            : "Certificate input is staged locally and will be used when you enable remote access.";

    public string RemoteFirewallSummary => Legacy.RemoteCreateFirewallRule
        ? "A Windows Firewall rule will be created or updated when you apply the settings."
        : "Firewall changes are disabled until you opt in.";

    public string RemoteFirewallActionSummary => Legacy.RemoteCreateFirewallRule
        ? "Firewall changes are part of the rollout and should be applied after the local self-test passes."
        : "Firewall changes are deferred; manual inbound access will still be required if you expose the listener later.";

    public string RemoteHostSummary => string.IsNullOrWhiteSpace(Legacy.RemotePublicHostname)
        ? "No public hostname is set. The bind address will be used for local exposure."
        : $"Public hostname: {Legacy.RemotePublicHostname}";

    public string RemoteIdentitySummary => Legacy.OwnerSummary;

    public string RemoteActionSummary => Legacy.RemoteAccessEnabled
        ? "Run the local self-test, confirm the certificate and bind posture, then restart the host and apply the firewall rule if you want inbound access."
        : "Keep the listener disabled while you prepare the certificate and 2FA prerequisites locally.";

    public string RemoteReadinessSummary => Legacy.RemoteAccessEnabled
        ? "Remote admin is configured for HTTPS exposure once the certificate and binding are validated."
        : "Desktop administration is ready; remote access stays off until you enable it here.";

    public string RemoteEnabledLabel => Legacy.RemoteAccessEnabled
        ? "Enabled: On"
        : "Enabled: Off";

    public string RemoteSelfTestHeadline
    {
        get
        {
            var checks = GetSelfTestLines();
            if (checks.Length == 0)
            {
                return "No self-test has been run yet.";
            }

            return checks.Any(line => line.Contains("failed", StringComparison.OrdinalIgnoreCase))
                ? "The latest self-test found at least one blocking issue."
                : "The latest self-test did not report a blocking issue.";
        }
    }

    public string RemoteSelfTestStats
    {
        get
        {
            var checks = GetSelfTestLines();
            if (checks.Length == 0)
            {
                return "No local checks captured yet.";
            }

            var warningCount = checks.Count(line => line.Contains("manual", StringComparison.OrdinalIgnoreCase) || line.Contains("requires administrative rights", StringComparison.OrdinalIgnoreCase));
            var blockingCount = checks.Count(line => line.Contains("failed", StringComparison.OrdinalIgnoreCase) || line.Contains("could not", StringComparison.OrdinalIgnoreCase) || line.Contains("is not", StringComparison.OrdinalIgnoreCase));
            return $"{checks.Length} check(s) recorded | {blockingCount} blocking signal(s) | {warningCount} follow-up reminder(s).";
        }
    }

    public string RemoteFirewallIntentSummary => Legacy.RemoteCreateFirewallRule
        ? "Firewall rule intent is enabled. Apply the rule after the certificate and bind settings pass the local checks."
        : "Firewall rule intent is off. Remote access will still need manual inbound allowance if you expose it later.";

    public string RemoteOperatorChecklistSummary => Legacy.RemoteAccessEnabled
        ? "Treat this as a rollout checklist: validate the certificate, verify the bind address, review the self-test output, and only then open the firewall posture."
        : "Treat this as a staging checklist: finish local prerequisites first, then decide whether the remote listener should stay off.";

    public string RemoteLocalOnlyGuidance => Legacy.RemoteAccessEnabled
        ? "The machine is no longer loopback-only. Keep exposure limited until you are comfortable with the certificate, port, and firewall posture."
        : "Nothing leaves the machine yet, which is the safest state for desktop-first administration.";

    public IReadOnlyList<RemoteSelfTestCheckViewModel> RemoteSelfTestChecks => GetSelfTestLines()
        .Select(ParseSelfTestCheck)
        .ToArray();

    public int RemoteSelfTestCheckCount => RemoteSelfTestChecks.Count;

    public int RemoteBlockingCheckCount => RemoteSelfTestChecks.Count(check => check.IsBlocking);

    public int RemoteFollowUpCheckCount => RemoteSelfTestChecks.Count(check => check.IsFollowUp);

    public int RemoteHealthyCheckCount => RemoteSelfTestChecks.Count(check => !check.IsBlocking && !check.IsFollowUp);

    public bool HasRemoteSelfTestChecks => RemoteSelfTestChecks.Count > 0;

    public bool HasNoRemoteSelfTestChecks => !HasRemoteSelfTestChecks;

    public string RemoteChecklistHeadline
    {
        get
        {
            if (!HasRemoteSelfTestChecks)
            {
                return "No self-test has been run yet.";
            }

            return RemoteBlockingCheckCount > 0
                ? "The latest self-test still has blocking items."
                : "The latest self-test looks actionable.";
        }
    }

    public string RemoteChecklistSummary
    {
        get
        {
            if (!HasRemoteSelfTestChecks)
            {
                return "Run the local self-test after saving remote settings to turn this panel into a rollout checklist.";
            }

            return $"{RemoteSelfTestCheckCount} check(s) recorded | {RemoteBlockingCheckCount} blocking | {RemoteFollowUpCheckCount} follow-up | {RemoteHealthyCheckCount} healthy.";
        }
    }

    public string RemoteNextStepSummary
    {
        get
        {
            if (!Legacy.RemoteAccessEnabled)
            {
                return "Keep remote access disabled if this machine is meant to stay local-only, or enable it only after your certificate path and owner 2FA are ready.";
            }

            var checks = GetSelfTestLines();
            return checks.Any(line => line.Contains("failed", StringComparison.OrdinalIgnoreCase))
                ? "Fix the blocking self-test issue first, then rerun the check before you expose HTTPS beyond the local machine."
                : "If the local checks look healthy, restart the host to apply HTTPS binding changes and only then consider firewall or router work.";
        }
    }

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
            e.PropertyName == nameof(MainWindowViewModel.RemoteSelfTestChecks))
        {
            OnPropertyChanged(nameof(RemoteStatusSummary));
            OnPropertyChanged(nameof(RemoteModeSummary));
            OnPropertyChanged(nameof(RemotePostureSummary));
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
    }

    private string[] GetSelfTestLines() =>
        string.IsNullOrWhiteSpace(Legacy.RemoteSelfTestChecks)
            ? []
            : Legacy.RemoteSelfTestChecks
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static RemoteSelfTestCheckViewModel ParseSelfTestCheck(string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("failed", StringComparison.Ordinal) ||
            normalized.Contains("could not", StringComparison.Ordinal) ||
            normalized.Contains("is not", StringComparison.Ordinal))
        {
            return new RemoteSelfTestCheckViewModel("Blocking", message, true, false);
        }

        if (normalized.Contains("manual", StringComparison.Ordinal) ||
            normalized.Contains("requires administrative rights", StringComparison.Ordinal) ||
            normalized.Contains("restart the host", StringComparison.Ordinal))
        {
            return new RemoteSelfTestCheckViewModel("Follow-up", message, false, true);
        }

        return new RemoteSelfTestCheckViewModel("Healthy", message, false, false);
    }

    public sealed record RemoteSelfTestCheckViewModel(
        string StatusLabel,
        string Message,
        bool IsBlocking,
        bool IsFollowUp);
}
