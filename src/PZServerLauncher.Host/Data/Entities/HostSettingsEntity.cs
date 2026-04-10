namespace PZServerLauncher.Host.Data.Entities;

public sealed class HostSettingsEntity
{
    public int Id { get; set; } = 1;

    public int LoopbackPort { get; set; } = 48231;

    public bool StartHostWithWindows { get; set; }

    public bool RemoteAccessEnabled { get; set; }

    public string RemoteBindAddress { get; set; } = "0.0.0.0";

    public int RemoteHttpsPort { get; set; } = 8443;

    public string? PublicHostname { get; set; }

    public string? CertificatePath { get; set; }

    public bool CreateFirewallRule { get; set; }

    public bool OwnerIsConfigured { get; set; }

    public string? OwnerUserId { get; set; }

    public string? OwnerUserName { get; set; }

    public DateTimeOffset? OwnerConfiguredAtUtc { get; set; }
}
