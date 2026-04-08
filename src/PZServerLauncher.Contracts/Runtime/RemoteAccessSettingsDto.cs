namespace PZServerLauncher.Contracts.Runtime;

public sealed record RemoteAccessSettingsDto(
    bool IsEnabled,
    string BindAddress,
    int HttpsPort,
    string? PublicHostname,
    string? CertificatePath,
    string? CertificatePassword,
    bool CreateFirewallRule);
