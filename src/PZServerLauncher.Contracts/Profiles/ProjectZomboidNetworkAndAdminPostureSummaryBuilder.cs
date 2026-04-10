namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidNetworkAndAdminPostureSummary(
    string AccessHeadline,
    string TrustHeadline,
    string IdentityAndSafetyHeadline,
    string VoiceHeadline,
    string RecoveryHeadline,
    string OperatorSummary,
    IReadOnlyList<string> Checklist);

public static class ProjectZomboidNetworkAndAdminPostureSummaryBuilder
{
    public static ProjectZomboidNetworkAndAdminPostureSummary Build(
        IReadOnlyDictionary<string, string?> values,
        bool requiresAdvancedFilesFallback,
        bool hasUnsavedChanges,
        int fieldErrorCount)
    {
        if (requiresAdvancedFilesFallback)
        {
            return new ProjectZomboidNetworkAndAdminPostureSummary(
                "Structured network posture is unavailable.",
                "Advanced Files is required for this server's network and admin settings.",
                "Identity, safety, and trust rules cannot be summarized safely while fallback is active.",
                "Voice posture is unavailable while fallback is active.",
                "Use the raw editor for recovery and unsupported syntax.",
                "Structured editing is disabled for this page. Route through Advanced Files before changing live trust, access, or voice settings.",
                ["Open Advanced Files for raw network/admin editing before you touch live access controls."]);
        }

        var bindIp = GetValue(values, ".network.bind-ip", "default bind");
        var autoWhitelist = ParseBool(values, ".network.auto-whitelist");
        var maxAccounts = GetValue(values, ".network.max-accounts-per-user", "0");
        var allowNonAscii = ParseBool(values, ".network.allow-non-ascii-username");
        var upnpEnabled = ParseBool(values, ".network.upnp");
        var pingLimit = GetValue(values, ".network.ping-limit", "100");

        var doLuaChecksum = ParseBool(values, ".network.do-lua-checksum");
        var steamVac = ParseBool(values, ".network.steam-vac", true);
        var kickFastPlayers = ParseBool(values, ".network.kick-fast-players");
        var denyLoginWhenOverloaded = ParseBool(values, ".network.deny-login-overloaded");
        var playerSaveOnDamage = ParseBool(values, ".network.player-save-on-damage");

        var displayUserName = ParseBool(values, ".network.display-user-name", true);
        var showFirstAndLastName = ParseBool(values, ".network.show-first-last-name");
        var mouseOverDisplayName = ParseBool(values, ".network.mouse-over-display-name", true);
        var hidePlayersBehindYou = ParseBool(values, ".network.hide-players-behind-you", true);
        var playerBumpPlayer = ParseBool(values, ".network.player-bump-player");
        var safetySystem = ParseBool(values, ".network.safety-system");
        var showSafety = ParseBool(values, ".network.show-safety");
        var safetyToggleTimer = GetValue(values, ".network.safety-toggle-timer", "0");
        var safetyCooldownTimer = GetValue(values, ".network.safety-cooldown-timer", "0");

        var voiceEnabled = ParseBool(values, ".network.voice-enabled");
        var voice3d = ParseBool(values, ".network.voice-3d");
        var voiceMinDistance = GetValue(values, ".network.voice-min-distance", "10");
        var voiceMaxDistance = GetValue(values, ".network.voice-max-distance", "100");

        var accountSummary = maxAccounts == "0"
            ? "unlimited accounts"
            : $"{maxAccounts} account(s) per user";
        var accessHeadline = $"Bind {bindIp} | whitelist {(autoWhitelist ? "auto-create" : "manual")} | {accountSummary} | usernames {(allowNonAscii ? "unicode allowed" : "ascii-focused")} | UPnP {(upnpEnabled ? "on" : "off")} | ping {pingLimit}.";

        var trustHeadline = $"VAC {(steamVac ? "on" : "off")} | Lua checksum {(doLuaChecksum ? "on" : "off")} | fast-player kicks {(kickFastPlayers ? "on" : "off")} | overload login {(denyLoginWhenOverloaded ? "denied" : "allowed")} | player save on damage {(playerSaveOnDamage ? "on" : "off")}.";

        var nameplateSummary = displayUserName
            ? showFirstAndLastName ? "full names" : "usernames"
            : mouseOverDisplayName ? "hover names" : "names hidden";
        var identityAndSafetyHeadline = $"Names {nameplateSummary} | rear cull {(hidePlayersBehindYou ? "on" : "off")} | bump {(playerBumpPlayer ? "on" : "off")} | safety {(safetySystem ? "enabled" : "off")} | icon {(showSafety ? "shown" : "hidden")} | toggle {safetyToggleTimer}s | cooldown {safetyCooldownTimer}s.";

        var voiceHeadline = voiceEnabled
            ? voice3d
                ? $"3D voice {voiceMinDistance}-{voiceMaxDistance} | in-world comms enabled."
                : "Global voice enabled | proximity attenuation disabled."
            : "Voice chat disabled for this server.";

        var recoveryHeadline = hasUnsavedChanges
            ? "Network and admin edits are staged locally but not applied yet."
            : fieldErrorCount > 0
                ? $"{fieldErrorCount} validation issue(s) need attention before the next save."
                : "Structured network/admin state is clean. Password fields remain write-only by design.";

        var operatorSummary = BuildOperatorSummary(
            hasUnsavedChanges,
            fieldErrorCount,
            doLuaChecksum,
            steamVac,
            autoWhitelist,
            denyLoginWhenOverloaded,
            safetySystem,
            voiceEnabled,
            voice3d);

        return new ProjectZomboidNetworkAndAdminPostureSummary(
            accessHeadline,
            trustHeadline,
            identityAndSafetyHeadline,
            voiceHeadline,
            recoveryHeadline,
            operatorSummary,
            BuildChecklist(
                hasUnsavedChanges,
                fieldErrorCount,
                doLuaChecksum,
                steamVac,
                autoWhitelist,
                denyLoginWhenOverloaded,
                allowNonAscii,
                voiceEnabled,
                voice3d,
                safetySystem));
    }

    public static ProjectZomboidNetworkAndAdminPostureSummary Empty() =>
        Build(
            new Dictionary<string, string?>(StringComparer.Ordinal),
            requiresAdvancedFilesFallback: false,
            hasUnsavedChanges: false,
            fieldErrorCount: 0);

    private static string BuildOperatorSummary(
        bool hasUnsavedChanges,
        int fieldErrorCount,
        bool doLuaChecksum,
        bool steamVac,
        bool autoWhitelist,
        bool denyLoginWhenOverloaded,
        bool safetySystem,
        bool voiceEnabled,
        bool voice3d)
    {
        if (hasUnsavedChanges)
        {
            return "Review the staged access and trust changes, then apply them before you rely on this page as the live posture.";
        }

        if (fieldErrorCount > 0)
        {
            return "Resolve the validation issues first so the next save does not push the server into a risky or ambiguous trust posture.";
        }

        if (!steamVac && !doLuaChecksum)
        {
            return "Both VAC and Lua checksum are relaxed. Confirm that this is deliberate before the next public session or mod rollout.";
        }

        if (!autoWhitelist && !denyLoginWhenOverloaded)
        {
            return "This server is favoring frictionless access. Make sure your moderation and operator coverage can absorb login spikes.";
        }

        if (voiceEnabled && !voice3d)
        {
            return "Voice is currently global rather than proximity-based, so community expectations should be called out in General or the welcome message.";
        }

        if (!safetySystem)
        {
            return "PvP safety signaling is disabled here. Cross-check General and Overview so the server's combat posture stays obvious to players.";
        }

        return "The page is in a healthy operator state. Use it to tune trust, safety, and voice posture, then confirm the result from Overview and Logs.";
    }

    private static IReadOnlyList<string> BuildChecklist(
        bool hasUnsavedChanges,
        int fieldErrorCount,
        bool doLuaChecksum,
        bool steamVac,
        bool autoWhitelist,
        bool denyLoginWhenOverloaded,
        bool allowNonAscii,
        bool voiceEnabled,
        bool voice3d,
        bool safetySystem)
    {
        var checklist = new List<string>();

        if (hasUnsavedChanges)
        {
            checklist.Add("Apply or discard the staged network/admin changes before leaving the page.");
        }

        if (fieldErrorCount > 0)
        {
            checklist.Add("Resolve the validation issues before the next structured save.");
        }

        if (!doLuaChecksum)
        {
            checklist.Add("Confirm the server really should allow clients without Lua checksum enforcement, especially on modded communities.");
        }

        if (!steamVac)
        {
            checklist.Add("Review the anti-cheat posture before the next public or high-population session.");
        }

        if (!autoWhitelist)
        {
            checklist.Add("If access is intentionally open, make sure operator moderation coverage still matches the expected join volume.");
        }

        if (!denyLoginWhenOverloaded)
        {
            checklist.Add("Check install and runtime capacity before a busy session because overload logins are currently allowed.");
        }

        if (allowNonAscii)
        {
            checklist.Add("Keep moderation and log review tooling ready for non-ASCII account names.");
        }

        if (voiceEnabled && !voice3d)
        {
            checklist.Add("Call out the global voice posture in your rules or welcome text so players are not surprised in session.");
        }

        if (!voiceEnabled)
        {
            checklist.Add("If voice is disabled, make sure your community knows what external comms path to use.");
        }

        if (!safetySystem)
        {
            checklist.Add("Double-check PvP expectations in Overview and General because the safety system is off.");
        }

        if (checklist.Count == 0)
        {
            checklist.Add("Review Overview and Logs after the next restart to confirm the live trust and voice posture matches this page.");
        }

        return checklist;
    }

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
