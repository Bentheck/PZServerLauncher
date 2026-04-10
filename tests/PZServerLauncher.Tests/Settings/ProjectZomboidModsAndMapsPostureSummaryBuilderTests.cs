using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidModsAndMapsPostureSummaryBuilderTests
{
    [Fact]
    public void Build_WithoutScanOrPresets_PromptsForValidationAndFallback()
    {
        var summary = ProjectZomboidModsAndMapsPostureSummaryBuilder.Build(
            new WorkshopPreset
            {
                WorkshopItemIds = ["1234567890"],
                EnabledModIds = ["ExampleMod"],
                MapFolders = ["RavenCreek"],
            },
            scanResult: null,
            namedPresetCount: 0,
            installDetected: true,
            cacheDetected: true,
            hasUnsavedChanges: false);

        Assert.Contains("Scanner posture is stale", summary.ValidationHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No named recovery loadout", summary.PresetLibraryHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.Contains("Run Scan Workshop", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.Checklist, item => item.Contains("named preset", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_WithDiagnostics_CategorizesBuckets()
    {
        var scanResult = new WorkshopScanResultDto(
            new WorkshopPreset
            {
                WorkshopItemIds = ["1234567890"],
                EnabledModIds = ["ExampleMod"],
                MapFolders = ["RavenCreek"],
            },
            [
                "Workshop input 'broken' is not a valid Steam Workshop URL or numeric id.",
                "Workshop item '1234567890' was listed more than once.",
                "Workshop item '9999999999' is not present in the local workshop cache.",
                "Enabled mod 'MissingMod' was not found in the local workshop content.",
                "Map folder 'MissingMap' was not found in the local workshop content.",
            ]);

        var summary = ProjectZomboidModsAndMapsPostureSummaryBuilder.Build(
            scanResult.Preset,
            scanResult,
            namedPresetCount: 1,
            installDetected: true,
            cacheDetected: true,
            hasUnsavedChanges: false);

        Assert.Equal(5, summary.DiagnosticCount);
        Assert.Contains(summary.DiagnosticBuckets, bucket => bucket.Title == "Invalid Workshop Input" && bucket.Count == 1);
        Assert.Contains(summary.DiagnosticBuckets, bucket => bucket.Title == "Duplicate Workshop IDs" && bucket.Count == 1);
        Assert.Contains(summary.DiagnosticBuckets, bucket => bucket.Title == "Missing Workshop Items" && bucket.Count == 1);
        Assert.Contains(summary.DiagnosticBuckets, bucket => bucket.Title == "Missing Enabled Mods" && bucket.Count == 1);
        Assert.Contains(summary.DiagnosticBuckets, bucket => bucket.Title == "Missing Map Folders" && bucket.Count == 1);
        Assert.Contains("scan issue", summary.ValidationHeadline, StringComparison.OrdinalIgnoreCase);
    }
}
