using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class RemoteAccessSettingsValidatorTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Validate_AcceptsMatchingCertificateHostname()
    {
        Directory.CreateDirectory(_tempRoot);
        var certificatePath = CreateCertificate("host.example.test", "secret");

        RemoteAccessSettingsValidator.Validate(new RemoteAccessSettingsDto(
            true,
            "0.0.0.0",
            8443,
            "host.example.test",
            certificatePath,
            "secret",
            true));
    }

    [Fact]
    public void Validate_RejectsMismatchedCertificateHostname()
    {
        Directory.CreateDirectory(_tempRoot);
        var certificatePath = CreateCertificate("host.example.test", "secret");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RemoteAccessSettingsValidator.Validate(new RemoteAccessSettingsDto(
                true,
                "0.0.0.0",
                8443,
                "wrong.example.test",
                certificatePath,
                "secret",
                true)));

        Assert.Contains("does not match the certificate DNS name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateCertificate(string dnsName, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={dnsName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName(dnsName);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var certificatePath = Path.Combine(_tempRoot, $"{dnsName}.pfx");
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password));
        return certificatePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
