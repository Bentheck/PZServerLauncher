using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class RemoteAccessDiagnosticsService(
    HostSettingsStore hostSettingsStore,
    HostBootstrapStateStore bootstrapStateStore)
{
    public async Task<RemoteAccessSelfTestResultDto> RunSelfTestAsync(
        RemoteAccessSettingsDto requestedSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedSettings);

        var checks = new List<string>();
        var success = true;
        var effectivePassword = await bootstrapStateStore.ResolveCertificatePasswordAsync(
            requestedSettings.CertificatePath,
            requestedSettings.CertificatePassword,
            cancellationToken);
        var effectiveSettings = requestedSettings with
        {
            CertificatePassword = effectivePassword,
        };

        try
        {
            RemoteAccessSettingsValidator.Validate(effectiveSettings);
            checks.Add("Certificate validation passed for the current remote access settings.");
        }
        catch (Exception ex)
        {
            checks.Add($"Certificate validation failed: {ex.Message}");
            return BuildFailure(checks, "Remote access self-test failed before the endpoint checks could run.");
        }

        if (!TryValidateLocalBindAddress(effectiveSettings.BindAddress, out var bindMessage))
        {
            checks.Add(bindMessage);
            return BuildFailure(checks, "Remote access self-test failed because the bind address is not available on this machine.");
        }

        checks.Add(bindMessage);

        var currentSettings = await hostSettingsStore.GetAsync(cancellationToken);
        var matchesActiveConfiguration = MatchesCurrentConfiguration(currentSettings.RemoteAccess, effectiveSettings);

        if (matchesActiveConfiguration && currentSettings.RemoteAccess.IsEnabled)
        {
            var liveProbe = await ProbeHttpsEndpointAsync(currentSettings, cancellationToken);
            checks.Add(liveProbe.Message);
            success &= liveProbe.Success;
        }
        else
        {
            var bindCheck = TryOpenPortProbe(effectiveSettings.BindAddress, effectiveSettings.HttpsPort, out var portMessage);
            checks.Add(portMessage);
            success &= bindCheck;

            if (success)
            {
                checks.Add("The saved HTTPS listener is not active for these exact settings yet. Restart the host after saving to validate the live endpoint.");
            }
        }

        if (effectiveSettings.CreateFirewallRule)
        {
            checks.Add("Firewall rule creation still requires administrative rights on Windows.");
        }

        checks.Add("Router and NAT forwarding remain manual in v1. This self-test only verifies the local machine and the host listener.");

        return new RemoteAccessSelfTestResultDto(
            success,
            success
                ? "Remote access self-test passed."
                : "Remote access self-test found at least one blocking issue.",
            checks);
    }

    private static bool TryValidateLocalBindAddress(string bindAddress, out string message)
    {
        if (!IPAddress.TryParse(bindAddress, out var parsed))
        {
            message = "The bind address is not a valid IPv4 address.";
            return false;
        }

        if (IPAddress.Any.Equals(parsed))
        {
            message = "Wildcard bind address 0.0.0.0 is valid and will listen on all local interfaces.";
            return true;
        }

        var localAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .ToHashSet();

        if (!localAddresses.Contains(parsed))
        {
            message = $"The bind address {bindAddress} is not assigned to an active local network interface.";
            return false;
        }

        message = $"The bind address {bindAddress} is present on a local network interface.";
        return true;
    }

    private static bool TryOpenPortProbe(string bindAddress, int port, out string message)
    {
        try
        {
            var address = IPAddress.Parse(bindAddress);
            using var listener = new TcpListener(IPAddress.Any.Equals(address) ? IPAddress.Any : address, port);
            listener.Start();
            message = $"TCP port {port} can be reserved on {bindAddress}.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"TCP port {port} could not be reserved on {bindAddress}: {ex.Message}";
            return false;
        }
    }

    private static bool MatchesCurrentConfiguration(RemoteAccessSettings current, RemoteAccessSettingsDto requested) =>
        current.IsEnabled == requested.IsEnabled &&
        string.Equals(current.BindAddress, requested.BindAddress, StringComparison.OrdinalIgnoreCase) &&
        current.HttpsPort == requested.HttpsPort &&
        string.Equals(current.PublicHostname ?? string.Empty, requested.PublicHostname ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(current.CertificatePath ?? string.Empty, requested.CertificatePath ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static async Task<(bool Success, string Message)> ProbeHttpsEndpointAsync(
        HostSettings currentSettings,
        CancellationToken cancellationToken)
    {
        var remoteSettings = currentSettings.RemoteAccess;
        var probeHost = remoteSettings.BindAddress == "0.0.0.0"
            ? "127.0.0.1"
            : remoteSettings.BindAddress;
        var probeUri = new Uri($"https://{probeHost}:{remoteSettings.HttpsPort}/");

        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(6),
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        try
        {
            using var response = await client.GetAsync(probeUri, cancellationToken);
            return (
                true,
                $"HTTPS probe reached {probeUri}. The listener responded with HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return (
                false,
                $"HTTPS probe to {probeUri} failed: {ex.Message}");
        }
    }

    private static RemoteAccessSelfTestResultDto BuildFailure(IReadOnlyList<string> checks, string summary) =>
        new(false, summary, checks);
}
