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
            ["b42.server.respawn-with-self"] = "true",
            ["b42.server.world-item-removal-hours"] = "36.5",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            generalValues,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal));

        Assert.Equal("Sleep allowed | respawn self on | cleanup 36.5h | safehouses enabled | factions enabled | trade UI off | fire spread disabled.", summary.ServerRulesSummary);
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
            ["b42.network.display-user-name"] = "false",
            ["b42.network.show-first-last-name"] = "false",
            ["b42.network.mouse-over-display-name"] = "true",
            ["b42.network.hide-players-behind-you"] = "true",
            ["b42.network.player-bump-player"] = "false",
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

        Assert.Equal("Bind 10.0.0.42 | VAC on | whitelist manual | safety enabled | names hover names | rear cull on | bump off | 3D voice 12-48.", summary.NetworkSummary);
        Assert.True(summary.IsVoiceEnabled);
        Assert.True(summary.IsSafetyEnabled);
    }

    [Fact]
    public void Build_SummarizesWorldSnapshot()
    {
        var sandboxValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.sandbox.zombies"] = "Normal",
            ["b42.sandbox.day-length"] = "1 Hour, 30 Minutes",
            ["b42.sandbox.helicopter"] = "Often",
            ["b42.sandbox.hours-for-loot-respawn"] = "6",
            ["b42.sandbox.enable-vehicles"] = "true",
            ["b42.sandbox.fire-spread"] = "false",
            ["b42.sandbox.hours-for-corpse-removal"] = "120.0",
            ["b42.sandbox.multi-hit"] = "true",
            ["b42.sandbox.bone-fracture"] = "false",
            ["b42.sandbox.attack-block-movements"] = "false",
            ["b42.sandbox.vehicle-easy-use"] = "true",
            ["b42.sandbox.player-damage-from-crash"] = "false",
            ["b42.sandbox.starter-kit"] = "true",
            ["b42.sandbox.nutrition"] = "false",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal),
            sandboxValues);

        Assert.Equal("Zombies Normal | day length 1 Hour, 30 Minutes | helicopter Often | loot respawn 6 | vehicles on | fire spread off | corpse cleanup 120.0h | multi-hit on | fractures off | attack lock off | easy vehicles on | crash damage off | starter kit on | nutrition off.", summary.WorldSummary);
    }

    [Fact]
    public void Build_SummarizesSandboxTuning()
    {
        var sandboxValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.sandbox.zombie-lore-speed"] = "Sprinter",
            ["b42.sandbox.zombie-lore-strength"] = "Tough",
            ["b42.sandbox.zombie-lore-transmission"] = "Blood and Saliva",
            ["b42.sandbox.zombie-lore-mortality"] = "Never",
            ["b42.sandbox.zombie-lore-reanimate"] = "Instant",
            ["b42.sandbox.zombie-lore-cognition"] = "Use Doors",
            ["b42.sandbox.zombie-lore-memory"] = "Long",
            ["b42.sandbox.zombie-lore-sight"] = "Eagle",
            ["b42.sandbox.zombie-lore-hearing"] = "Pinpoint",
            ["b42.sandbox.zombie-house-alarm-triggering"] = "true",
            ["b42.sandbox.damage-construction"] = "true",
            ["b42.sandbox.drag-down"] = "false",
            ["b42.sandbox.zombie-lunge"] = "true",
        };

        var summary = ProjectZomboidProfilePostureSummaryBuilder.Build(
            "Nightingale",
            new Dictionary<string, string?>(StringComparer.Ordinal),
            new Dictionary<string, string?>(StringComparer.Ordinal),
            sandboxValues);

        Assert.Equal("Zombie lore speed Sprinter | strength Tough | transmission Blood and Saliva | mortality Never | reanimate Instant | cognition Use Doors | memory Long | sight Eagle | hearing Pinpoint | alarm on | build thump on | drag off | fence on.", summary.SandboxTuningSummary);
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
