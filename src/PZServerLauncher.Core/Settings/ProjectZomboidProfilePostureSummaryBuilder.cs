namespace PZServerLauncher.Core.Settings;

public sealed record ProjectZomboidProfilePostureSummary(
    string CommunitySummary,
    string ServerRulesSummary,
    string NetworkSummary,
    string WorldSummary,
    string WelcomeSummary,
    bool IsPubliclyListed,
    bool IsOpenAccess,
    bool IsPvpEnabled,
    bool IsVoiceEnabled,
    bool IsSafetyEnabled);

public static class ProjectZomboidProfilePostureSummaryBuilder
{
    public static ProjectZomboidProfilePostureSummary Build(
        string displayName,
        IReadOnlyDictionary<string, string?> generalValues,
        IReadOnlyDictionary<string, string?> networkValues,
        IReadOnlyDictionary<string, string?> sandboxValues)
    {
        var publicName = GetValue(generalValues, ".server.public-name");
        var maxPlayers = GetValue(generalValues, ".server.max-players", "32");
        var isPublic = ParseBool(generalValues, ".server.public");
        var isOpen = ParseBool(generalValues, ".server.open");
        var pvpEnabled = ParseBool(generalValues, ".server.pvp", true);
        var safetyEnabled = ParseBool(networkValues, ".network.safety-system", true);
        var voiceEnabled = ParseBool(networkValues, ".network.voice-enabled", true);

        var communitySummary = $"{(string.IsNullOrWhiteSpace(publicName) ? displayName : publicName)} | {maxPlayers} slots | {(isPublic ? "public listing on" : "private listing")} | {(isOpen ? "open access" : "password-gated")} | PvP {(pvpEnabled ? "on" : "off")}.";

        var sleepAllowed = ParseBool(generalValues, ".server.sleep-allowed");
        var sleepNeeded = ParseBool(generalValues, ".server.sleep-needed");
        var playerSafehouse = ParseBool(generalValues, ".server.player-safehouse");
        var factionEnabled = ParseBool(generalValues, ".server.faction-enabled");
        var tradeUi = ParseBool(generalValues, ".server.allow-trade-ui");
        var noFire = ParseBool(generalValues, ".server.no-fire");
        var serverRulesSummary = $"Sleep {(sleepAllowed ? (sleepNeeded ? "required" : "allowed") : "disabled")} | safehouses {(playerSafehouse ? "enabled" : "off")} | factions {(factionEnabled ? "enabled" : "off")} | trade UI {(tradeUi ? "enabled" : "off")} | fire spread {(noFire ? "disabled" : "enabled")}.";

        var bindIp = GetValue(networkValues, ".network.bind-ip", "default bind");
        var steamVac = ParseBool(networkValues, ".network.steam-vac", true);
        var autoWhitelist = ParseBool(networkValues, ".network.auto-whitelist");
        var voice3d = ParseBool(networkValues, ".network.voice-3d", true);
        var voiceMin = GetValue(networkValues, ".network.voice-min-distance", "10");
        var voiceMax = GetValue(networkValues, ".network.voice-max-distance", "100");
        var voiceSummary = voiceEnabled
            ? voice3d
                ? $"3D voice {voiceMin}-{voiceMax}"
                : "global voice"
            : "voice disabled";
        var networkSummary = $"Bind {bindIp} | VAC {(steamVac ? "on" : "off")} | whitelist {(autoWhitelist ? "auto-create" : "manual")} | safety {(safetyEnabled ? "enabled" : "off")} | {voiceSummary}.";

        var zombies = GetValue(sandboxValues, ".sandbox.zombies", "4");
        var dayLength = GetValue(sandboxValues, ".sandbox.day-length", "3");
        var waterShutoff = GetValue(sandboxValues, ".sandbox.water-shut-modifier", "500");
        var electricityShutoff = GetValue(sandboxValues, ".sandbox.electricity-shut-modifier", "480");
        var lootRespawn = GetValue(sandboxValues, ".sandbox.loot-respawn", "2");
        var starterKit = ParseBool(sandboxValues, ".sandbox.starter-kit");
        var nutrition = ParseBool(sandboxValues, ".sandbox.nutrition");
        var worldSummary = $"Zombies {zombies} | day length {dayLength} | water shutoff {waterShutoff} days | electricity shutoff {electricityShutoff} days | loot respawn {lootRespawn} | starter kit {(starterKit ? "on" : "off")} | nutrition {(nutrition ? "on" : "off")}.";

        var welcomeMessage = GetValue(generalValues, ".server.welcome-message");
        var welcomeSummary = string.IsNullOrWhiteSpace(welcomeMessage)
            ? "No welcome message configured yet."
            : $"Welcome: {welcomeMessage.ReplaceLineEndings(" ").Replace("  ", " ", StringComparison.Ordinal).Trim()}";

        return new ProjectZomboidProfilePostureSummary(
            communitySummary,
            serverRulesSummary,
            networkSummary,
            worldSummary,
            welcomeSummary,
            isPublic,
            isOpen,
            pvpEnabled,
            voiceEnabled,
            safetyEnabled);
    }

    public static ProjectZomboidProfilePostureSummary Unavailable(string displayName) =>
        new(
            $"{displayName} | structured posture unavailable.",
            "Server rules summary is temporarily unavailable.",
            "Network posture summary is temporarily unavailable.",
            "World snapshot is temporarily unavailable.",
            "Welcome message summary is temporarily unavailable.",
            false,
            false,
            false,
            false,
            false);

    private static string GetValue(IReadOnlyDictionary<string, string?> values, string suffix, string fallback = "")
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? fallback : values[key] ?? fallback;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string?> values, string suffix, bool fallback = false)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is not null && bool.TryParse(values[key], out var parsed) ? parsed : fallback;
    }
}
