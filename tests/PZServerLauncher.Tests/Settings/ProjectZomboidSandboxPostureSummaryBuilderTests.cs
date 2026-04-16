using PZServerLauncher.Contracts.Profiles;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidSandboxPostureSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesWorldPressureAndRecovery()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.sandbox.zombies"] = "Normal",
            ["b42.sandbox.distribution"] = "Urban Focused",
            ["b42.sandbox.day-length"] = "1 Hour, 30 Minutes",
            ["b42.sandbox.start-time"] = "9 AM",
            ["b42.sandbox.start-month"] = "July",
            ["b42.sandbox.population-multiplier"] = "Normal",
            ["b42.sandbox.population-peak-multiplier"] = "High",
            ["b42.sandbox.population-peak-day"] = "28",
            ["b42.sandbox.respawn-hours"] = "0.0",
            ["b42.sandbox.follow-sound-distance"] = "100",
            ["b42.sandbox.rally-group-size"] = "20",
            ["b42.sandbox.perishable-food-loot"] = "Custom",
            ["b42.sandbox.ranged-weapons-loot"] = "Custom",
            ["b42.sandbox.other-loot"] = "Normal",
            ["b42.sandbox.hours-for-loot-respawn"] = "0",
            ["b42.sandbox.farming"] = "1.0",
            ["b42.sandbox.nature-abundance"] = "Normal",
            ["b42.sandbox.food-spoilage"] = "Normal",
            ["b42.sandbox.end-regen"] = "Normal",
            ["b42.sandbox.helicopter"] = "Once",
            ["b42.sandbox.meta-event"] = "Sometimes",
            ["b42.sandbox.sleeping-event"] = "Never",
            ["b42.sandbox.rain"] = "Normal",
            ["b42.sandbox.temperature"] = "Normal",
            ["b42.sandbox.alarm"] = "Sometimes",
            ["b42.sandbox.locked-houses"] = "Very Often",
            ["b42.sandbox.water-shut-modifier"] = "14",
            ["b42.sandbox.electricity-shut-modifier"] = "14",
            ["b42.sandbox.multi-hit"] = "false",
            ["b42.sandbox.fire-spread"] = "true",
            ["b42.sandbox.enable-vehicles"] = "true",
            ["b42.sandbox.starter-kit"] = "false",
            ["b42.sandbox.nutrition"] = "true",
            ["b42.sandbox.bone-fracture"] = "true",
            ["b42.sandbox.player-damage-from-crash"] = "true",
            ["b42.sandbox.vehicle-easy-use"] = "false",
        };

        var summary = ProjectZomboidSandboxPostureSummaryBuilder.Build(
            values,
            requiresAdvancedFilesFallback: false,
            hasUnsavedChanges: true,
            fieldErrorCount: 0);

        Assert.Contains("July", summary.WorldStateHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Population Normalx to Highx by day 28", summary.ZombiePressureHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Loot F/W/O Custom/Custom/Normal", summary.SurvivalEconomyHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Helicopter Once", summary.EventAndClimateHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Multi-hit off", summary.SurvivorRulesHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("staged locally", summary.RecoveryHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.Contains("Save a draft or apply", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_FallbackModeRoutesToAdvancedFiles()
    {
        var summary = ProjectZomboidSandboxPostureSummaryBuilder.Build(
            new Dictionary<string, string?>(StringComparer.Ordinal),
            requiresAdvancedFilesFallback: true,
            hasUnsavedChanges: false,
            fieldErrorCount: 0);

        Assert.Contains("unavailable", summary.WorldStateHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Advanced Files", summary.OperatorSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(summary.Checklist);
    }
}
