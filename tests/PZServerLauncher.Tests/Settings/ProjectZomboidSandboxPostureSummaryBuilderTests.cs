using PZServerLauncher.Contracts.Profiles;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidSandboxPostureSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesWorldPressureAndRecovery()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b41.sandbox.zombies"] = "3",
            ["b41.sandbox.distribution"] = "2",
            ["b41.sandbox.day-length"] = "3",
            ["b41.sandbox.start-time"] = "5",
            ["b41.sandbox.start-month"] = "7",
            ["b41.sandbox.population-multiplier"] = "2.0",
            ["b41.sandbox.population-peak-multiplier"] = "3.0",
            ["b41.sandbox.population-peak-day"] = "14",
            ["b41.sandbox.respawn-hours"] = "48.0",
            ["b41.sandbox.follow-sound-distance"] = "200",
            ["b41.sandbox.rally-group-size"] = "30",
            ["b41.sandbox.food-loot"] = "2",
            ["b41.sandbox.weapon-loot"] = "1",
            ["b41.sandbox.other-loot"] = "2",
            ["b41.sandbox.loot-respawn"] = "5",
            ["b41.sandbox.farming"] = "4",
            ["b41.sandbox.nature-abundance"] = "2",
            ["b41.sandbox.food-rot-speed"] = "2",
            ["b41.sandbox.end-regen"] = "4",
            ["b41.sandbox.helicopter"] = "3",
            ["b41.sandbox.meta-event"] = "2",
            ["b41.sandbox.sleeping-event"] = "3",
            ["b41.sandbox.rain"] = "4",
            ["b41.sandbox.temperature"] = "2",
            ["b41.sandbox.alarm"] = "5",
            ["b41.sandbox.locked-houses"] = "4",
            ["b41.sandbox.water-shut-modifier"] = "60",
            ["b41.sandbox.electricity-shut-modifier"] = "45",
            ["b41.sandbox.multi-hit"] = "true",
            ["b41.sandbox.fire-spread"] = "false",
            ["b41.sandbox.enable-vehicles"] = "true",
            ["b41.sandbox.starter-kit"] = "true",
            ["b41.sandbox.nutrition"] = "true",
            ["b41.sandbox.bone-fracture"] = "true",
            ["b41.sandbox.player-damage-from-crash"] = "true",
            ["b41.sandbox.vehicle-easy-use"] = "false",
        };

        var summary = ProjectZomboidSandboxPostureSummaryBuilder.Build(
            values,
            requiresAdvancedFilesFallback: false,
            hasUnsavedChanges: true,
            fieldErrorCount: 0);

        Assert.Contains("July", summary.WorldStateHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Population 2.0x to 3.0x by day 14", summary.ZombiePressureHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Loot F/W/O 2/1/2", summary.SurvivalEconomyHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Helicopter 3", summary.EventAndClimateHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Multi-hit on", summary.SurvivorRulesHeadline, StringComparison.OrdinalIgnoreCase);
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
