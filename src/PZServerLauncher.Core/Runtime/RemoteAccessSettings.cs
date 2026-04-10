namespace PZServerLauncher.Core.Runtime;

public sealed record RemoteAccessSettings
{
    public bool IsEnabled { get; init; }

    public string BindAddress { get; init; } = "0.0.0.0";

    public int HttpsPort { get; init; } = 8443;

    public string? PublicHostname { get; init; }

    public string? CertificatePath { get; init; }

    public bool CreateFirewallRule { get; init; }

    public bool RequiresHostRestart { get; init; }
}
