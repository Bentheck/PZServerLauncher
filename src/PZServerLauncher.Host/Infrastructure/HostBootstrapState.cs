namespace PZServerLauncher.Host.Infrastructure;

public sealed record HostBootstrapState
{
    public int LoopbackPort { get; init; } = 48231;

    public string ProtectedLocalApiToken { get; init; } = string.Empty;

    public bool RemoteAccessEnabled { get; init; }

    public string RemoteBindAddress { get; init; } = "0.0.0.0";

    public int RemoteHttpsPort { get; init; } = 8443;

    public string? PublicHostname { get; init; }

    public string? CertificatePath { get; init; }

    public string? ProtectedCertificatePassword { get; init; }

    public bool CreateFirewallRule { get; init; }
}
