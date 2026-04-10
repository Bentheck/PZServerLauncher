using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidModsAndMapsDiagnosticBucket(
    string Title,
    int Count,
    string Summary);

public sealed record ProjectZomboidModsAndMapsPostureSummary(
    int WorkshopCount,
    int ModCount,
    int MapCount,
    int NamedPresetCount,
    int DiagnosticCount,
    string LoadoutHeadline,
    string ValidationHeadline,
    string PresetLibraryHeadline,
    string MapChainHeadline,
    string QueueIntegritySummary,
    string ScannerSummary,
    string RecoverySummary,
    string OperatorSummary,
    IReadOnlyList<ProjectZomboidModsAndMapsDiagnosticBucket> DiagnosticBuckets,
    IReadOnlyList<string> Checklist);

public static class ProjectZomboidModsAndMapsPostureSummaryBuilder
{
    public static ProjectZomboidModsAndMapsPostureSummary Build(
        WorkshopPreset preset,
        WorkshopScanResultDto? scanResult,
        int namedPresetCount,
        bool installDetected,
        bool cacheDetected,
        bool hasUnsavedChanges)
    {
        var workshopCount = preset.WorkshopItemIds.Count;
        var modCount = preset.EnabledModIds.Count;
        var mapCount = preset.MapFolders.Count;
        var diagnostics = scanResult?.Diagnostics ?? [];

        var invalidWorkshopCount = CountDiagnostics(diagnostics, "not a valid Steam Workshop URL or numeric id");
        var duplicateWorkshopCount = CountDiagnostics(diagnostics, "listed more than once");
        var missingWorkshopCount = CountDiagnostics(diagnostics, "is not present in the local workshop cache");
        var missingModCount = CountDiagnostics(diagnostics, "Enabled mod '");
        var missingMapCount = CountDiagnostics(diagnostics, "Map folder '");
        var cacheDiagnosticCount = CountDiagnostics(diagnostics, "No local workshop content directory");

        var loadoutHeadline = workshopCount == 0 && modCount == 0 && mapCount == 0
            ? "No live loadout queued yet."
            : $"{workshopCount} workshop | {modCount} mods | {mapCount} maps in the active stack.";

        var validationHeadline = scanResult is null
            ? cacheDetected
                ? "Scanner posture is stale until you run a validation pass."
                : "Local workshop cache is not ready yet."
            : diagnostics.Count == 0
                ? "Latest scan says the saved preset matches local workshop content."
                : $"{diagnostics.Count} scan issue(s): {missingWorkshopCount} workshop, {missingModCount} mods, {missingMapCount} maps.";

        var presetLibraryHeadline = namedPresetCount == 0
            ? "No named recovery loadout saved yet."
            : $"{namedPresetCount} named preset(s) ready for rollback or reuse.";

        var mapChainHeadline = mapCount == 0
            ? "Vanilla map chain only."
            : mapCount == 1
                ? "1 custom map folder is pinned into load order."
                : $"{mapCount} custom map folders are chained into server load order.";

        var queueIntegritySummary = hasUnsavedChanges
            ? "The editor is ahead of the saved live preset. Apply or discard before you trust scanner posture."
            : "The editor matches the saved server preset.";

        var scannerSummary = scanResult is null
            ? cacheDetected
                ? "Run a scan after any install or preset change so the saved queue stays aligned with local workshop content."
                : "Scan coverage is blocked until the install path exposes a local workshop cache."
            : diagnostics.Count == 0
                ? "Normalization and local content checks are clean."
                : $"Normalization surfaced {invalidWorkshopCount + duplicateWorkshopCount} input issue(s) and {missingWorkshopCount + missingModCount + missingMapCount} local content mismatch(es).";

        var recoverySummary = namedPresetCount == 0
            ? "Save the current stack as a named preset so you can recover a known-good loadout after workshop churn."
            : hasUnsavedChanges
                ? "Named presets are available, but the current editor state has not been saved into the live preset yet."
                : "Named presets are ready if you need to roll back the live stack.";

        var operatorSummary = BuildOperatorSummary(
            installDetected,
            cacheDetected,
            hasUnsavedChanges,
            scanResult,
            diagnostics.Count,
            namedPresetCount,
            workshopCount,
            modCount,
            mapCount);

        var checklist = BuildChecklist(
            installDetected,
            cacheDetected,
            hasUnsavedChanges,
            scanResult,
            namedPresetCount,
            workshopCount,
            modCount,
            mapCount,
            invalidWorkshopCount,
            duplicateWorkshopCount,
            missingWorkshopCount,
            missingModCount,
            missingMapCount);

        var diagnosticBuckets = BuildBuckets(
            invalidWorkshopCount,
            duplicateWorkshopCount,
            missingWorkshopCount,
            missingModCount,
            missingMapCount,
            cacheDiagnosticCount);

        return new ProjectZomboidModsAndMapsPostureSummary(
            workshopCount,
            modCount,
            mapCount,
            namedPresetCount,
            diagnostics.Count,
            loadoutHeadline,
            validationHeadline,
            presetLibraryHeadline,
            mapChainHeadline,
            queueIntegritySummary,
            scannerSummary,
            recoverySummary,
            operatorSummary,
            diagnosticBuckets,
            checklist);
    }

    public static ProjectZomboidModsAndMapsPostureSummary Empty() =>
        Build(WorkshopPreset.Empty, null, 0, installDetected: false, cacheDetected: false, hasUnsavedChanges: false);

    private static string BuildOperatorSummary(
        bool installDetected,
        bool cacheDetected,
        bool hasUnsavedChanges,
        WorkshopScanResultDto? scanResult,
        int diagnosticCount,
        int namedPresetCount,
        int workshopCount,
        int modCount,
        int mapCount)
    {
        if (!installDetected)
        {
            return "Install the dedicated server first so the launcher can validate workshop content against a real file footprint.";
        }

        if (workshopCount == 0 && modCount == 0 && mapCount == 0)
        {
            return "Seed the loadout with at least one workshop item or map folder, then validate it against the install.";
        }

        if (!cacheDetected)
        {
            return "The queue is editable, but the local workshop cache is still missing so scanner posture is incomplete.";
        }

        if (hasUnsavedChanges)
        {
            return "Apply or discard the editor changes before running another scan so diagnostics match the saved live preset.";
        }

        if (scanResult is null)
        {
            return "Run a scan to normalize the saved queue and compare it with the local workshop cache.";
        }

        if (diagnosticCount > 0)
        {
            return "Resolve the scanner issues or accept them deliberately before the next restart so the live stack is predictable.";
        }

        return namedPresetCount == 0
            ? "The live preset is clean. Save a named fallback now so rollback is one click away."
            : "The live preset is validated, normalized, and backed by named fallbacks.";
    }

    private static IReadOnlyList<string> BuildChecklist(
        bool installDetected,
        bool cacheDetected,
        bool hasUnsavedChanges,
        WorkshopScanResultDto? scanResult,
        int namedPresetCount,
        int workshopCount,
        int modCount,
        int mapCount,
        int invalidWorkshopCount,
        int duplicateWorkshopCount,
        int missingWorkshopCount,
        int missingModCount,
        int missingMapCount)
    {
        var checklist = new List<string>();

        if (!installDetected)
        {
            checklist.Add("Install or import the server footprint before you trust workshop validation.");
            return checklist;
        }

        if (workshopCount == 0 && modCount == 0 && mapCount == 0)
        {
            checklist.Add("Paste a workshop URL or numeric ID to seed the loadout.");
        }

        if (hasUnsavedChanges)
        {
            checklist.Add("Apply or discard local queue edits before the next scan.");
        }

        if (!cacheDetected)
        {
            checklist.Add("Download or install workshop content so the scanner has a local cache to inspect.");
        }
        else if (scanResult is null)
        {
            checklist.Add("Run Scan Workshop to normalize the queue and validate the local cache.");
        }

        if (invalidWorkshopCount > 0 || duplicateWorkshopCount > 0)
        {
            checklist.Add("Normalize workshop input so only unique numeric IDs remain.");
        }

        if (missingWorkshopCount > 0)
        {
            checklist.Add("Pull the missing workshop items into the local cache before restart.");
        }

        if (missingModCount > 0)
        {
            checklist.Add("Align enabled mod IDs with mod.info entries found in the install.");
        }

        if (missingMapCount > 0)
        {
            checklist.Add("Fix map load order so every folder exists in installed workshop content.");
        }

        if (namedPresetCount == 0 && (workshopCount > 0 || modCount > 0 || mapCount > 0))
        {
            checklist.Add("Save the current stack as a named preset for rollback coverage.");
        }

        if (checklist.Count == 0)
        {
            checklist.Add("Keep the saved queue aligned with the local cache after every mod or map install change.");
        }

        return checklist;
    }

    private static IReadOnlyList<ProjectZomboidModsAndMapsDiagnosticBucket> BuildBuckets(
        int invalidWorkshopCount,
        int duplicateWorkshopCount,
        int missingWorkshopCount,
        int missingModCount,
        int missingMapCount,
        int cacheDiagnosticCount)
    {
        var buckets = new List<ProjectZomboidModsAndMapsDiagnosticBucket>();

        if (invalidWorkshopCount > 0)
        {
            buckets.Add(new ProjectZomboidModsAndMapsDiagnosticBucket(
                "Invalid Workshop Input",
                invalidWorkshopCount,
                "At least one workshop line was not a valid Steam URL or numeric ID."));
        }

        if (duplicateWorkshopCount > 0)
        {
            buckets.Add(new ProjectZomboidModsAndMapsDiagnosticBucket(
                "Duplicate Workshop IDs",
                duplicateWorkshopCount,
                "The same workshop item was queued more than once."));
        }

        if (missingWorkshopCount > 0)
        {
            buckets.Add(new ProjectZomboidModsAndMapsDiagnosticBucket(
                "Missing Workshop Items",
                missingWorkshopCount,
                "Some queued workshop IDs are not present in the local cache."));
        }

        if (missingModCount > 0)
        {
            buckets.Add(new ProjectZomboidModsAndMapsDiagnosticBucket(
                "Missing Enabled Mods",
                missingModCount,
                "Some enabled mod IDs do not exist in installed workshop content."));
        }

        if (missingMapCount > 0)
        {
            buckets.Add(new ProjectZomboidModsAndMapsDiagnosticBucket(
                "Missing Map Folders",
                missingMapCount,
                "Some map folders in load order are missing locally."));
        }

        if (cacheDiagnosticCount > 0)
        {
            buckets.Add(new ProjectZomboidModsAndMapsDiagnosticBucket(
                "Cache Detection",
                cacheDiagnosticCount,
                "The scanner could not find a local workshop content directory for this install."));
        }

        return buckets;
    }

    private static int CountDiagnostics(IEnumerable<string> diagnostics, string fragment) =>
        diagnostics.Count(message => message.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
