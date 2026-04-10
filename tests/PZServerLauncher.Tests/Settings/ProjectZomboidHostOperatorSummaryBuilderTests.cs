using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidHostOperatorSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesFleetExposureAndRecovery()
    {
        var settings = new HostSettings
        {
            LoopbackPort = 48233,
            StartHostWithWindows = true,
            RemoteAccess = new RemoteAccessSettings
            {
                IsEnabled = true,
                BindAddress = "10.0.0.42",
                HttpsPort = 8443,
            },
            OwnerBootstrap = new OwnerBootstrapState(true, "owner-1", "Bentheck", DateTimeOffset.UtcNow),
        };

        var summary = ProjectZomboidHostOperatorSummaryBuilder.Build(
            settings,
            [
                new ProjectZomboidHostManagedProfileSnapshot("Riverside", "Build 42", "Running", true, true, true, true, "16261 / 27015"),
                new ProjectZomboidHostManagedProfileSnapshot("Muldraugh", "Build 41", "Stopped", false, true, false, true, "16262 / 27016"),
            ]);

        Assert.Equal("Loopback 48233 | Windows startup enabled.", summary.LifecycleHeadline);
        Assert.Contains("2 profile(s) loaded | 2 installed | 1 running.", summary.FleetHeadline);
        Assert.Contains("HTTPS staged for 10.0.0.42:8443", summary.ExposureHeadline);
        Assert.Contains("Bentheck", summary.SecurityHeadline);
        Assert.Contains("recovery coverage", summary.OperatorSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.IsBlocking && item.Message.Contains("recovery archive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_UsesEmptyRosterGuidanceWhenNoProfilesExist()
    {
        var summary = ProjectZomboidHostOperatorSummaryBuilder.Build(
            new HostSettings(),
            Array.Empty<ProjectZomboidHostManagedProfileSnapshot>());

        Assert.Contains("No Project Zomboid profiles", summary.FleetHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Single(summary.Checklist);
        Assert.Contains("first profile", summary.NextStepSummary, StringComparison.OrdinalIgnoreCase);
    }
}
