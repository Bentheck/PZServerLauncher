using System.ComponentModel;

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

    public string RemoteCertificateSummary => string.IsNullOrWhiteSpace(Legacy.RemoteCertificatePath)
        ? "No certificate path has been selected yet."
        : $"Certificate path: {Legacy.RemoteCertificatePath}";

    public string RemoteFirewallSummary => Legacy.RemoteCreateFirewallRule
        ? "A Windows Firewall rule will be created or updated when you apply the settings."
        : "Firewall changes are disabled until you opt in.";

    public string RemoteHostSummary => string.IsNullOrWhiteSpace(Legacy.RemotePublicHostname)
        ? "No public hostname is set. The bind address will be used for local exposure."
        : $"Public hostname: {Legacy.RemotePublicHostname}";

    public string RemoteActionSummary => "Run the local self-test before exposing HTTPS remotely, then apply the firewall rule if you want inbound access.";

    public string RemoteReadinessSummary => Legacy.RemoteAccessEnabled
        ? "Remote admin is configured for HTTPS exposure once the certificate and binding are validated."
        : "Desktop administration is ready; remote access stays off until you enable it here.";

    public string RemoteEnabledLabel => Legacy.RemoteAccessEnabled
        ? "Enabled: On"
        : "Enabled: Off";

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
            OnPropertyChanged(nameof(RemoteCertificateSummary));
            OnPropertyChanged(nameof(RemoteFirewallSummary));
            OnPropertyChanged(nameof(RemoteHostSummary));
            OnPropertyChanged(nameof(RemoteActionSummary));
            OnPropertyChanged(nameof(RemoteReadinessSummary));
            OnPropertyChanged(nameof(RemoteEnabledLabel));
        }
    }
}
