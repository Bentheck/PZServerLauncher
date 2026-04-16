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
            ownerBootstrapConfigured: true,
            ["Certificate validation passed.", "Manual router forwarding is still required."]);

        Assert.Equal("Remote HTTPS is staged for exposure.", summary.ModeHeadline);
        Assert.Equal("Planned endpoint: https://pz.example.com:8443", summary.EndpointHeadline);
        Assert.Contains("Certificate path", summary.CertificateHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.IsFollowUp && item.Message.Contains("router forwarding", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("restart", summary.NextStepSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_BlocksWhenOwnerBootstrapIsMissing()
    {
        var summary = ProjectZomboidRemoteAccessRolloutSummaryBuilder.Build(
            new RemoteAccessSettings(),
            ownerBootstrapConfigured: false,
            Array.Empty<string>());

        Assert.Contains("bootstrap", summary.ReadinessHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.IsBlocking && item.Message.Contains("owner bootstrap", StringComparison.OrdinalIgnoreCase));
    }
}
