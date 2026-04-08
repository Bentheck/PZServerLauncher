using System.ComponentModel;
using System.Linq;

namespace PZServerLauncher.App.ViewModels;

public sealed class RemoteAccessWorkspaceViewModel : WorkspacePageViewModelBase
{
    public RemoteAccessWorkspaceViewModel(MainWindowViewModel legacy)
        : base(
            "Remote Access",
            "Optional HTTPS admin setup, local validation, and Windows Firewall rule control for the web workspace.",
            "Remote access settings are in sync.",
            ["HTTPS binding", "Local self-test", "Firewall rule", "Desktop-driven setup"])
    {
        Legacy = legacy;
        Legacy.PropertyChanged += OnLegacyPropertyChanged;
    }

    public MainWindowViewModel Legacy { get; }

    public string RemoteStatusSummary => Legacy.RemoteWizardStatus;

    public string RemoteBindingSummary => Legacy.RemoteAccessEnabled
        ? $"HTTPS will bind to {Legacy.RemoteBindAddress}:{Legacy.RemoteHttpsPort}."
        : "Remote access is disabled, so the host is loopback-only.";

    public string RemoteEndpointSummary => Legacy.RemoteAccessEnabled
        ? $"Planned endpoint: https://{(string.IsNullOrWhiteSpace(Legacy.RemotePublicHostname) ? Legacy.RemoteBindAddress : Legacy.RemotePublicHostname)}:{Legacy.RemoteHttpsPort}"
        : "No remote endpoint is currently exposed.";

    public string RemoteCertificateSummary => string.IsNullOrWhiteSpace(Legacy.RemoteCertificatePath)
        ? "No certificate path has been selected yet."
        : $"Certificate path: {Legacy.RemoteCertificatePath}";

    public string RemoteFirewallSummary => Legacy.RemoteCreateFirewallRule
        ? "A Windows Firewall rule will be created or updated when you apply the settings."
        : "Firewall changes are disabled until you opt in.";

    public string RemoteHostSummary => string.IsNullOrWhiteSpace(Legacy.RemotePublicHostname)
        ? "No public hostname is set. The bind address will be used for local exposure."
        : $"Public hostname: {Legacy.RemotePublicHostname}";

    public string RemoteIdentitySummary => Legacy.OwnerSummary;

    public string RemoteActionSummary => "Run the local self-test before exposing HTTPS remotely, then apply the firewall rule if you want inbound access.";

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

    public IReadOnlyList<RemoteSelfTestCheckViewModel> RemoteSelfTestChecks => GetSelfTestLines()
        .Select(ParseSelfTestCheck)
        .ToArray();

    public bool HasRemoteSelfTestChecks => RemoteSelfTestChecks.Count > 0;

    public bool HasNoRemoteSelfTestChecks => !HasRemoteSelfTestChecks;

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
            OnPropertyChanged(nameof(RemoteBindingSummary));
            OnPropertyChanged(nameof(RemoteEndpointSummary));
            OnPropertyChanged(nameof(RemoteCertificateSummary));
            OnPropertyChanged(nameof(RemoteFirewallSummary));
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
