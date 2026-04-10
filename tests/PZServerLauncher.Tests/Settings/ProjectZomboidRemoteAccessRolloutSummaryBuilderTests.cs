using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidRemoteAccessRolloutSummaryBuilderTests
{
    [Fact]
    public void Build_CombinesRolloutPrerequisitesAndSelfTestSignals()
    {
        var settings = new RemoteAccessSettings
        {
            IsEnabled = true,
            BindAddress = "10.0.0.42",
            HttpsPort = 8443,
            PublicHostname = "pz.example.com",
            CertificatePath = @"C:\certs\pz-admin.pfx",
            CreateFirewallRule = true,
            RequiresHostRestart = true,
        };

        var summary = ProjectZomboidRemoteAccessRolloutSummaryBuilder.Build(
            settings,
            ownerAccountsWithTwoFactor: 1,
            ["Certificate validation passed.", "Manual router forwarding is still required."]);

        Assert.Equal("Remote HTTPS is staged for exposure.", summary.ModeHeadline);
        Assert.Equal("Planned endpoint: https://pz.example.com:8443", summary.EndpointHeadline);
        Assert.Contains("Certificate path", summary.CertificateHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.IsFollowUp && item.Message.Contains("router forwarding", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("restart", summary.NextStepSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_BlocksWhenOwnerTwoFactorIsMissing()
    {
        var summary = ProjectZomboidRemoteAccessRolloutSummaryBuilder.Build(
            new RemoteAccessSettings(),
            ownerAccountsWithTwoFactor: 0,
            Array.Empty<string>());

        Assert.Contains("2FA", summary.ReadinessHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.IsBlocking && item.Message.Contains("TOTP", StringComparison.OrdinalIgnoreCase));
    }
}
