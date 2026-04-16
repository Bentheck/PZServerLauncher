using System.Net;
using System.Security.Cryptography.X509Certificates;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.Host.Services;

public static class RemoteAccessSettingsValidator
{
    public static void Validate(RemoteAccessSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!IPAddress.TryParse(settings.BindAddress, out _))
        {
            throw new InvalidOperationException("Remote bind address must be a valid IPv4 address.");
        }

        if (settings.HttpsPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("Remote HTTPS port must be between 1 and 65535.");
        }

        if (!settings.IsEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.CertificatePath))
        {
            throw new InvalidOperationException("A certificate path is required before remote access can be enabled.");
        }

        if (!File.Exists(settings.CertificatePath))
        {
            throw new FileNotFoundException("The configured certificate file was not found.", settings.CertificatePath);
        }

        using var certificate = string.IsNullOrWhiteSpace(settings.CertificatePassword)
            ? X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, password: null, X509KeyStorageFlags.EphemeralKeySet)
            : X509CertificateLoader.LoadPkcs12FromFile(settings.CertificatePath, settings.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);

        if (!string.IsNullOrWhiteSpace(settings.PublicHostname))
        {
            var certificateName = certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false);
            if (!string.Equals(certificateName, settings.PublicHostname, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The configured public hostname does not match the certificate DNS name.");
            }
        }
    }
}
