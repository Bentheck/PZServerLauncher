using PZServerLauncher.Contracts.Profiles;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidNetworkAndAdminPostureSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesAccessTrustIdentityAndVoice()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.network.bind-ip"] = "10.0.0.42",
            ["b42.network.auto-whitelist"] = "true",
            ["b42.network.max-accounts-per-user"] = "2",
            ["b42.network.allow-non-ascii-username"] = "true",
            ["b42.network.upnp"] = "false",
            ["b42.network.ping-limit"] = "180",
            ["b42.network.do-lua-checksum"] = "true",
            ["b42.network.steam-vac"] = "false",
            ["b42.network.kick-fast-players"] = "true",
            ["b42.network.deny-login-overloaded"] = "false",
            ["b42.network.player-save-on-damage"] = "true",
            ["b42.network.display-user-name"] = "false",
            ["b42.network.show-first-last-name"] = "false",
            ["b42.network.mouse-over-display-name"] = "true",
            ["b42.network.hide-players-behind-you"] = "true",
            ["b42.network.player-bump-player"] = "false",
            ["b42.network.safety-system"] = "true",
            ["b42.network.show-safety"] = "true",
            ["b42.network.safety-toggle-timer"] = "15",
            ["b42.network.safety-cooldown-timer"] = "45",
            ["b42.network.voice-enabled"] = "true",
            ["b42.network.voice-3d"] = "true",
            ["b42.network.voice-min-distance"] = "12",
            ["b42.network.voice-max-distance"] = "48",
        };

        var summary = ProjectZomboidNetworkAndAdminPostureSummaryBuilder.Build(
            values,
            requiresAdvancedFilesFallback: false,
            hasUnsavedChanges: false,
            fieldErrorCount: 0);

        Assert.Equal("Bind 10.0.0.42 | whitelist auto-create | 2 account(s) per user | usernames unicode allowed | UPnP off | ping 180.", summary.AccessHeadline);
        Assert.Equal("VAC off | Lua checksum on | fast-player kicks on | overload login allowed | player save on damage on.", summary.TrustHeadline);
        Assert.Equal("Names hover names | rear cull on | bump off | safety enabled | icon shown | toggle 15s | cooldown 45s.", summary.IdentityAndSafetyHeadline);
        Assert.Equal("3D voice 12-48 | in-world comms enabled.", summary.VoiceHeadline);
        Assert.Contains("anti-cheat posture", string.Join(' ', summary.Checklist));
    }

    [Fact]
    public void Build_UsesFallbackSummaryWhenStructuredEditingIsUnavailable()
    {
        var summary = ProjectZomboidNetworkAndAdminPostureSummaryBuilder.Build(
            new Dictionary<string, string?>(StringComparer.Ordinal),
            requiresAdvancedFilesFallback: true,
            hasUnsavedChanges: false,
            fieldErrorCount: 0);

        Assert.Equal("Structured network posture is unavailable.", summary.AccessHeadline);
        Assert.Single(summary.Checklist);
        Assert.Contains("Advanced Files", summary.OperatorSummary);
    }
}
