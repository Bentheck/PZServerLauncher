using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidProfilePostureSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesCommunityPosture()
    {
        var generalValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.server.public"] = "true",
            ["b42.server.open"] = "false",
            ["b42.server.pvp"] = "true",
            ["b42.server.max-players"] = "24",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            generalValues,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal));

        Assert.Equal("Nightingale | 24 slots | public listing on | password-gated | PvP on.", summary.CommunitySummary);
        Assert.True(summary.IsPubliclyListed);
        Assert.True(summary.IsPvpEnabled);
    }

    [Fact]
    public void Build_SummarizesServerRules()
    {
        var generalValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.server.sleep-allowed"] = "true",
            ["b42.server.sleep-needed"] = "false",
            ["b42.server.player-safehouse"] = "true",
            ["b42.server.faction-enabled"] = "true",
            ["b42.server.allow-trade-ui"] = "false",
            ["b42.server.no-fire"] = "true",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            generalValues,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal));

        Assert.Equal("Sleep allowed | safehouses enabled | factions enabled | trade UI off | fire spread disabled.", summary.ServerRulesSummary);
    }

    [Fact]
    public void Build_SummarizesNetworkPosture()
    {
        var networkValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.network.bind-ip"] = "10.0.0.42",
            ["b42.network.steam-vac"] = "true",
            ["b42.network.auto-whitelist"] = "false",
            ["b42.network.safety-system"] = "true",
            ["b42.network.voice-enabled"] = "true",
            ["b42.network.voice-3d"] = "true",
            ["b42.network.voice-min-distance"] = "12",
            ["b42.network.voice-max-distance"] = "48",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            new Dictionary<string, string?>(StringComparer.Ordinal),
            networkValues,
            new Dictionary<string, string?>(StringComparer.Ordinal));

        Assert.Equal("Bind 10.0.0.42 | VAC on | whitelist manual | safety enabled | 3D voice 12-48.", summary.NetworkSummary);
        Assert.True(summary.IsVoiceEnabled);
        Assert.True(summary.IsSafetyEnabled);
    }

    [Fact]
    public void Build_SummarizesWorldSnapshot()
    {
        var sandboxValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.sandbox.zombies"] = "5",
            ["b42.sandbox.day-length"] = "9",
            ["b42.sandbox.helicopter"] = "4",
            ["b42.sandbox.loot-respawn"] = "2",
            ["b42.sandbox.enable-vehicles"] = "true",
            ["b42.sandbox.fire-spread"] = "false",
            ["b42.sandbox.hours-for-corpse-removal"] = "120",
            ["b42.sandbox.multi-hit"] = "true",
            ["b42.sandbox.starter-kit"] = "true",
            ["b42.sandbox.nutrition"] = "false",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal),
            sandboxValues);

        Assert.Equal("Zombies 5 | day length 9 | helicopter 4 | loot respawn 2 | vehicles on | fire spread off | corpse cleanup 120h | multi-hit on | starter kit on | nutrition off.", summary.WorldSummary);
    }

    [Fact]
    public void Build_FlattensWelcomeMessage()
    {
        var generalValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.server.welcome-message"] = "Welcome survivor!\r\nStay alive.\nBring snacks.",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            generalValues,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal));

        Assert.Equal("Welcome: Welcome survivor! Stay alive. Bring snacks.", summary.WelcomeSummary);
    }
}
