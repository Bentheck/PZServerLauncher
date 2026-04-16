namespace PZServerLauncher.Core.Profiles;

public static class WorkshopPresetMergeHelper
{
    public static WorkshopPreset Append(
        WorkshopPreset preset,
        IReadOnlyList<string> workshopItemIds,
        IReadOnlyList<string> enabledModIds,
        IReadOnlyList<string> mapFolders)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return new WorkshopPreset
        {
            WorkshopItemIds = AppendDistinct(preset.WorkshopItemIds, workshopItemIds),
            EnabledModIds = AppendDistinct(preset.EnabledModIds, enabledModIds),
            MapFolders = AppendDistinct(preset.MapFolders, mapFolders),
        };
    }

    public static bool IsQueued(
        WorkshopPreset preset,
        string workshopId,
        IReadOnlyList<string> modIds,
        IReadOnlyList<string> mapFolders)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return preset.WorkshopPresetContains(workshopId) ||
               modIds.Any(preset.EnabledModIds.ContainsIgnoreCase) ||
               mapFolders.Any(preset.MapFolders.ContainsIgnoreCase);
    }

    private static IReadOnlyList<string> AppendDistinct(IReadOnlyList<string> existing, IReadOnlyList<string> additions)
    {
        var merged = new List<string>(existing.Count + additions.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in existing.Concat(additions))
        {
            var candidate = value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                merged.Add(candidate);
            }
        }

        return merged;
    }

    private static bool ContainsIgnoreCase(this IReadOnlyList<string> values, string value) =>
        values.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));

    private static bool WorkshopPresetContains(this WorkshopPreset preset, string workshopId) =>
        preset.WorkshopItemIds.ContainsIgnoreCase(workshopId);
}
