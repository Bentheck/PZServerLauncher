namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidSandboxPostureSummary(
    string WorldStateHeadline,
    string ZombiePressureHeadline,
    string SurvivalEconomyHeadline,
    string EventAndClimateHeadline,
    string SurvivorRulesHeadline,
    string RecoveryHeadline,
    string OperatorSummary,
    IReadOnlyList<string> Checklist);

public static class ProjectZomboidSandboxPostureSummaryBuilder
{
    public static ProjectZomboidSandboxPostureSummary Build(
        IReadOnlyDictionary<string, string?> values,
        bool requiresAdvancedFilesFallback,
        bool hasUnsavedChanges,
        int fieldErrorCount)
    {
        if (requiresAdvancedFilesFallback)
        {
            return new ProjectZomboidSandboxPostureSummary(
                "Structured sandbox posture is unavailable.",
                "Raw SandboxVars.lua editing is required for this file.",
                "Structured survival and economy summaries are unavailable while fallback is active.",
                "World-event and climate posture is unavailable while fallback is active.",
                "Survivor rule posture is unavailable while fallback is active.",
                "Use Advanced Files for raw recovery and unsupported syntax.",
                "Structured editing is disabled for this Sandbox file. Route through Advanced Files instead of trusting a partial save path.",
                ["Open Advanced Files for raw SandboxVars.lua editing and recovery."]);
        }

        var zombies = GetValue(values, ".sandbox.zombies", "4");
        var distribution = GetValue(values, ".sandbox.distribution", "1");
        var dayLength = GetValue(values, ".sandbox.day-length", "3");
        var startTime = GetValue(values, ".sandbox.start-time", "2");
        var startMonth = GetValue(values, ".sandbox.start-month", "7");

        var popMultiplier = GetValue(values, ".sandbox.population-multiplier", "1.0");
        var peakMultiplier = GetValue(values, ".sandbox.population-peak-multiplier", "1.5");
        var peakDay = GetValue(values, ".sandbox.population-peak-day", "28");
        var respawnHours = GetValue(values, ".sandbox.respawn-hours", "72.0");
        var followSound = GetValue(values, ".sandbox.follow-sound-distance", "100");
        var rallySize = GetValue(values, ".sandbox.rally-group-size", "20");

        var foodLoot = GetValue(values, ".sandbox.food-loot", "4");
        var weaponLoot = GetValue(values, ".sandbox.weapon-loot", "2");
        var otherLoot = GetValue(values, ".sandbox.other-loot", "3");
        var lootRespawn = GetValue(values, ".sandbox.loot-respawn", "2");
        var farming = GetValue(values, ".sandbox.farming", "1");
        var nature = GetValue(values, ".sandbox.nature-abundance", "3");
        var foodRot = GetValue(values, ".sandbox.food-rot-speed", "5");
        var endRegen = GetValue(values, ".sandbox.end-regen", "3");

        var helicopter = GetValue(values, ".sandbox.helicopter", "2");
        var metaEvent = GetValue(values, ".sandbox.meta-event", "1");
        var sleepingEvent = GetValue(values, ".sandbox.sleeping-event", "1");
        var rain = GetValue(values, ".sandbox.rain", "3");
        var temperature = GetValue(values, ".sandbox.temperature", "3");
        var alarm = GetValue(values, ".sandbox.alarm", "6");
        var lockedHouses = GetValue(values, ".sandbox.locked-houses", "6");
        var waterShut = GetValue(values, ".sandbox.water-shut-modifier", "500");
        var powerShut = GetValue(values, ".sandbox.electricity-shut-modifier", "480");

        var multiHit = ParseBool(values, ".sandbox.multi-hit");
        var fireSpread = ParseBool(values, ".sandbox.fire-spread", true);
        var vehiclesEnabled = ParseBool(values, ".sandbox.enable-vehicles", true);
        var starterKit = ParseBool(values, ".sandbox.starter-kit");
        var nutrition = ParseBool(values, ".sandbox.nutrition");
        var boneFracture = ParseBool(values, ".sandbox.bone-fracture", true);
        var crashDamage = ParseBool(values, ".sandbox.player-damage-from-crash", true);
        var easyVehicles = ParseBool(values, ".sandbox.vehicle-easy-use");

        var worldStateHeadline = $"Zombies {zombies} | distribution {distribution} | {FormatDayLength(dayLength)} | start {FormatStartMonth(startMonth)} {FormatStartTime(startTime)}.";
        var zombiePressureHeadline = $"Population {popMultiplier}x to {peakMultiplier}x by day {peakDay} | respawn {respawnHours}h | rally {rallySize} | sound pull {followSound}.";
        var survivalEconomyHeadline = $"Loot F/W/O {foodLoot}/{weaponLoot}/{otherLoot} | loot respawn {lootRespawn} | farming {farming} | nature {nature} | food rot {foodRot} | endurance {endRegen}.";
        var eventAndClimateHeadline = $"Helicopter {helicopter} | meta {metaEvent} | sleep events {sleepingEvent} | rain {rain} | temperature {temperature} | alarms {alarm} | locked houses {lockedHouses} | water {waterShut} | power {powerShut}.";
        var survivorRulesHeadline = $"Multi-hit {(multiHit ? "on" : "off")} | fire spread {(fireSpread ? "on" : "off")} | vehicles {(vehiclesEnabled ? "on" : "off")} | easy vehicles {(easyVehicles ? "on" : "off")} | fractures {(boneFracture ? "on" : "off")} | crash damage {(crashDamage ? "on" : "off")} | starter kit {(starterKit ? "on" : "off")} | nutrition {(nutrition ? "on" : "off")}.";

        var recoveryHeadline = hasUnsavedChanges
            ? "Sandbox edits are staged locally but not written yet."
            : fieldErrorCount > 0
                ? $"{fieldErrorCount} validation issue(s) need attention before the next save."
                : "Structured Sandbox state is clean and ready for another save or draft capture.";

        var operatorSummary = hasUnsavedChanges
            ? "Review the high-pressure zombie, event, and survival changes, then save or draft them before you leave the page."
            : fieldErrorCount > 0
                ? "Resolve the validation issues first so the next structured save does not force you into raw-file recovery."
                : "Use this page to shape world pressure and survivor friction, then confirm the server’s runtime posture from Overview and Logs.";

        return new ProjectZomboidSandboxPostureSummary(
            worldStateHeadline,
            zombiePressureHeadline,
            survivalEconomyHeadline,
            eventAndClimateHeadline,
            survivorRulesHeadline,
            recoveryHeadline,
            operatorSummary,
            BuildChecklist(hasUnsavedChanges, fieldErrorCount, vehiclesEnabled, helicopter, lootRespawn, starterKit));
    }

    private static IReadOnlyList<string> BuildChecklist(
        bool hasUnsavedChanges,
        int fieldErrorCount,
        bool vehiclesEnabled,
        string helicopter,
        string lootRespawn,
        bool starterKit)
    {
        var checklist = new List<string>();

        if (hasUnsavedChanges)
        {
            checklist.Add("Save a draft or apply the staged Sandbox changes before switching to another page.");
        }

        if (fieldErrorCount > 0)
        {
            checklist.Add("Clear the Sandbox validation issues before the next structured save.");
        }

        if (!vehiclesEnabled)
        {
            checklist.Add("Confirm your Mods & Maps loadout and map assumptions still make sense with vehicles disabled.");
        }

        if (!string.Equals(helicopter, "1", StringComparison.Ordinal))
        {
            checklist.Add("Review the helicopter and meta-event pace against your intended early-game pressure.");
        }

        if (!string.Equals(lootRespawn, "1", StringComparison.Ordinal))
        {
            checklist.Add("Keep loot respawn aligned with server rules so long-running worlds do not drift into over-abundance.");
        }

        if (starterKit)
        {
            checklist.Add("If Starter Kit is enabled, make sure General and Welcome messaging match the easier onboarding posture.");
        }

        if (checklist.Count == 0)
        {
            checklist.Add("Review Logs and Overview after the next restart to confirm the world pressure feels right in live play.");
        }

        return checklist;
    }

    private static string GetValue(IReadOnlyDictionary<string, string?> values, string suffix, string fallback)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? fallback : values[key] ?? fallback;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string?> values, string suffix, bool fallback = false)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is not null && bool.TryParse(values[key], out var parsed) ? parsed : fallback;
    }

    private static string FormatDayLength(string value) => value switch
    {
        "1" => "15-minute days",
        "2" => "30-minute days",
        "3" => "1-hour days",
        "4" => "2-hour days",
        "5" => "3-hour days",
        "6" => "4-hour days",
        "7" => "5-hour days",
        "8" => "12-hour days",
        "9" => "24-hour days",
        _ => $"day length {value}"
    };

    private static string FormatStartTime(string value) => value switch
    {
        "1" => "07:00 start",
        "2" => "09:00 start",
        "3" => "12:00 start",
        "4" => "14:00 start",
        "5" => "17:00 start",
        "6" => "21:00 start",
        "7" => "00:00 start",
        "8" => "02:00 start",
        "9" => "05:00 start",
        _ => $"time {value}"
    };

    private static string FormatStartMonth(string value) => value switch
    {
        "1" => "January",
        "2" => "February",
        "3" => "March",
        "4" => "April",
        "5" => "May",
        "6" => "June",
        "7" => "July",
        "8" => "August",
        "9" => "September",
        "10" => "October",
        "11" => "November",
        "12" => "December",
        _ => $"month {value}"
    };
}
