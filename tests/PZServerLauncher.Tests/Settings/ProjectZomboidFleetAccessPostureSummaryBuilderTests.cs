using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidFleetAccessPostureSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesFleetAccessAndTrust()
    {
        var postures = new[]
        {
            new ProjectZomboidProfilePostureSummary("", "", "", "", "", "", true, true, true, true, false),
            new ProjectZomboidProfilePostureSummary("", "", "", "", "", "", true, false, false, false, true),
            new ProjectZomboidProfilePostureSummary("", "", "", "", "", "", false, false, true, true, true),
        };

        var summary = ProjectZomboidFleetAccessPostureSummaryBuilder.Build(postures, remoteAccessEnabled: false);

        Assert.Equal(3, summary.ProfileCount);
        Assert.Equal(2, summary.PublicProfileCount);
        Assert.Equal(1, summary.OpenAccessCount);
        Assert.Equal(1, summary.PublicOpenWithoutSafetyCount);
        Assert.Equal("2 public | 1 private | 1 open access | 2 password-gated.", summary.AccessHeadline);
        Assert.Contains("public/open profile(s) are missing PvP safety", summary.OperatorSummary);
        Assert.Contains(summary.Checklist, item => item.Contains("remote access", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_ReturnsBootstrapGuidanceWhenFleetIsEmpty()
    {
        var summary = ProjectZomboidFleetAccessPostureSummaryBuilder.Build(
            Array.Empty<ProjectZomboidProfilePostureSummary>(),
            remoteAccessEnabled: true);

        Assert.Equal(0, summary.ProfileCount);
        Assert.Contains("first server profile", summary.OperatorSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(summary.Checklist);
    }
}
