using System.Text.RegularExpressions;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Host.Services;

public sealed partial class WorkshopPresetScannerService
{
    public IReadOnlyList<string> ResolveWorkshopItemIds(
        string? installDirectory,
        WorkshopPreset preset,
        IReadOnlyList<string>? fallbackWorkshopItemIds = null)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var enabledModIds = NormalizeList(preset.EnabledModIds);
        var mapFolders = NormalizeList(preset.MapFolders);
        var fallbackDiagnostics = new List<string>();
        var normalizedFallbackWorkshopIds = NormalizeWorkshopIds(
            fallbackWorkshopItemIds ?? preset.WorkshopItemIds,
            fallbackDiagnostics);
        var discoveredItems = DiscoverWorkshopItems(installDirectory);
        if (discoveredItems.Count == 0)
        {
            return normalizedFallbackWorkshopIds;
        }

        var resolvedWorkshopItemIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var modId in enabledModIds)
        {
            foreach (var item in discoveredItems.Where(candidate => candidate.ModIds.Contains(modId, StringComparer.OrdinalIgnoreCase)))
            {
                if (seen.Add(item.WorkshopId))
                {
                    resolvedWorkshopItemIds.Add(item.WorkshopId);
                }
            }
        }

        foreach (var mapFolder in mapFolders)
        {
            foreach (var item in discoveredItems.Where(candidate => candidate.MapFolders.Contains(mapFolder, StringComparer.OrdinalIgnoreCase)))
            {
                if (seen.Add(item.WorkshopId))
                {
                    resolvedWorkshopItemIds.Add(item.WorkshopId);
                }
            }
        }

        var discoveredWorkshopIds = discoveredItems
            .Select(item => item.WorkshopId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var workshopItemId in normalizedFallbackWorkshopIds.Where(id => !discoveredWorkshopIds.Contains(id)))
        {
            if (seen.Add(workshopItemId))
            {
                resolvedWorkshopItemIds.Add(workshopItemId);
            }
        }

        return resolvedWorkshopItemIds;
    }

    public WorkshopScanResultDto Scan(string? installDirectory, WorkshopPreset preset)
    {
        var diagnostics = new List<string>();
        var normalizedWorkshopIds = NormalizeWorkshopIds(preset.WorkshopItemIds, diagnostics);
        var enabledModIds = NormalizeList(preset.EnabledModIds);
        var mapFolders = NormalizeList(preset.MapFolders);

        var discoveredItems = DiscoverWorkshopItems(installDirectory);
        var discoveredWorkshopItems = discoveredItems
            .Select(item => item.WorkshopId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var discoveredMods = discoveredItems
            .SelectMany(item => item.ModIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var discoveredMaps = discoveredItems
            .SelectMany(item => item.MapFolders)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (discoveredItems.Count == 0)
        {
            diagnostics.Add("No local workshop content directory was detected for this install path.");
        }

        foreach (var workshopId in normalizedWorkshopIds)
        {
            if (!discoveredWorkshopItems.Contains(workshopId))
            {
                diagnostics.Add($"Workshop item '{workshopId}' is not present in the local workshop cache.");
            }
        }

        foreach (var modId in enabledModIds)
        {
            if (!discoveredMods.Contains(modId))
            {
                diagnostics.Add($"Enabled mod '{modId}' was not found in the local workshop content.");
            }
        }

        foreach (var mapFolder in mapFolders)
        {
            if (!discoveredMaps.Contains(mapFolder))
            {
                diagnostics.Add($"Map folder '{mapFolder}' was not found in the local workshop content.");
            }
        }

        return new WorkshopScanResultDto(
            new WorkshopPreset
            {
                WorkshopItemIds = normalizedWorkshopIds,
                EnabledModIds = enabledModIds,
                MapFolders = mapFolders,
            },
            diagnostics);
    }

    private static IReadOnlyList<string> NormalizeWorkshopIds(IReadOnlyList<string> values, ICollection<string> diagnostics)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var candidate = value.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var workshopId = candidate;
            var urlMatch = WorkshopUrlRegex().Match(candidate);
            if (urlMatch.Success)
            {
                workshopId = urlMatch.Groups["id"].Value;
            }

            if (!DigitsOnlyRegex().IsMatch(workshopId))
            {
                diagnostics.Add($"Workshop input '{value}' is not a valid Steam Workshop URL or numeric id.");
                continue;
            }

            if (!seen.Add(workshopId))
            {
                diagnostics.Add($"Workshop item '{workshopId}' was listed more than once.");
                continue;
            }

            normalized.Add(workshopId);
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string> values)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var candidate = value.Trim();
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                normalized.Add(candidate);
            }
        }

        return normalized;
    }

    private static IEnumerable<string> SplitCsv(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<DiscoveredWorkshopItem> DiscoverWorkshopItems(string? installDirectory)
    {
        var workshopRoot = ResolveWorkshopRoot(installDirectory);
        if (workshopRoot is null)
        {
            return [];
        }

        var discoveredItems = new List<DiscoveredWorkshopItem>();
        foreach (var itemDirectory in Directory.GetDirectories(workshopRoot))
        {
            var workshopId = Path.GetFileName(itemDirectory);
            if (string.IsNullOrWhiteSpace(workshopId))
            {
                continue;
            }

            var modIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mapFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var modInfoPath in Directory.GetFiles(itemDirectory, "mod.info", SearchOption.AllDirectories))
            {
                foreach (var line in File.ReadLines(modInfoPath))
                {
                    if (line.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
                    {
                        modIds.Add(line["id=".Length..].Trim());
                    }
                    else if (line.StartsWith("map=", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var map in SplitCsv(line["map=".Length..]))
                        {
                            mapFolders.Add(map);
                        }
                    }
                }

                var mediaMapsDirectory = Path.Combine(Path.GetDirectoryName(modInfoPath)!, "media", "maps");
                if (Directory.Exists(mediaMapsDirectory))
                {
                    foreach (var mapDirectory in Directory.GetDirectories(mediaMapsDirectory))
                    {
                        mapFolders.Add(Path.GetFileName(mapDirectory));
                    }
                }
            }

            discoveredItems.Add(new DiscoveredWorkshopItem(workshopId, modIds.ToArray(), mapFolders.ToArray()));
        }

        return discoveredItems;
    }

    private static string? ResolveWorkshopRoot(string? installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return null;
        }

        var direct = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600");
        if (Directory.Exists(direct))
        {
            return direct;
        }

        return Directory.GetDirectories(installDirectory, "108600", SearchOption.AllDirectories)
            .FirstOrDefault(path => path.Contains($"{Path.DirectorySeparatorChar}workshop{Path.DirectorySeparatorChar}content{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"[?&]id=(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WorkshopUrlRegex();

    [GeneratedRegex(@"^\d+$", RegexOptions.Compiled)]
    private static partial Regex DigitsOnlyRegex();
}

internal sealed record DiscoveredWorkshopItem(
    string WorkshopId,
    IReadOnlyList<string> ModIds,
    IReadOnlyList<string> MapFolders);
