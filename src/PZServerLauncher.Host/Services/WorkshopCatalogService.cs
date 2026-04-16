using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Memory;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Host.Services;

public sealed partial class WorkshopCatalogService(
    IMemoryCache memoryCache,
    SteamWorkshopApiClient steamWorkshopApiClient,
    WorkshopBrowserSettingsStore settingsStore)
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task<WorkshopCatalogSearchResultDto> SearchAsync(
        ServerProfile profile,
        WorkshopPreset currentPreset,
        WorkshopCatalogSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var mode = request.SearchMode;
        var filter = request.SearchFilter;
        var normalizedQuery = request.Query.Trim();
        var diagnostics = new List<string>();
        var hasApiKey = !string.IsNullOrWhiteSpace(await settingsStore.GetSteamWebApiKeyAsync(cancellationToken));
        var localItems = GetLocalItems(profile);

        var localResults = mode is WorkshopCatalogSearchMode.Local or WorkshopCatalogSearchMode.Both
            ? SearchLocal(profile, currentPreset, normalizedQuery, Math.Clamp(request.Take, 1, 50), localItems)
            : [];

        var merged = localResults.ToDictionary(item => item.WorkshopId, StringComparer.OrdinalIgnoreCase);

        if (mode is WorkshopCatalogSearchMode.Steam or WorkshopCatalogSearchMode.Both)
        {
            if (hasApiKey)
            {
                var apiKey = await settingsStore.GetSteamWebApiKeyAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    foreach (var item in await steamWorkshopApiClient.SearchAsync(apiKey, normalizedQuery, request.Take, cancellationToken))
                    {
                        merged[item.WorkshopId] = MergeItems(
                            merged.GetValueOrDefault(item.WorkshopId),
                            BuildSteamItem(profile, currentPreset, item));
                    }

                    foreach (var collection in await steamWorkshopApiClient.SearchCollectionsAsync(apiKey, normalizedQuery, request.Take, cancellationToken))
                    {
                        var collectionSummary = new SteamWorkshopCollection(
                            collection.WorkshopId,
                            collection.Title,
                            collection.Description,
                            collection.PreviewUrl,
                            collection.Tags,
                            collection.ChildWorkshopIds);
                        merged[collection.WorkshopId] = MergeItems(
                            merged.GetValueOrDefault(collection.WorkshopId),
                            BuildCollectionItem(profile, currentPreset, collectionSummary, localItems, WorkshopCatalogItemSource.Steam));
                    }
                }
            }
            else
            {
                diagnostics.Add("Steam search is unavailable until a Steam Web API user key is configured. You can still search the local cache or paste a Workshop ID manually.");
            }
        }

        var manualWorkshopId = NormalizeWorkshopLookup(normalizedQuery);
        if (!string.IsNullOrWhiteSpace(manualWorkshopId))
        {
            var collection = await steamWorkshopApiClient.GetCollectionAsync(manualWorkshopId, cancellationToken);
            if (collection is not null)
            {
                merged[collection.WorkshopId] = MergeItems(
                    merged.GetValueOrDefault(collection.WorkshopId),
                    BuildCollectionItem(profile, currentPreset, collection, localItems, WorkshopCatalogItemSource.Details));
            }
            else
            {
                var detail = await steamWorkshopApiClient.GetDetailsAsync(manualWorkshopId, cancellationToken);
                if (detail is not null)
                {
                    merged[detail.WorkshopId] = MergeItems(
                        merged.GetValueOrDefault(detail.WorkshopId),
                        BuildDetailItem(profile, currentPreset, detail));
                }
            }
        }

        var results = merged.Values
            .Where(item => MatchesFilter(item, filter))
            .OrderByDescending(item => item.IsQueued)
            .ThenByDescending(item => item.IsInstalledLocally)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(request.Take, 1, 50))
            .ToArray();

        return new WorkshopCatalogSearchResultDto(normalizedQuery, mode, hasApiKey, results, diagnostics);
    }

    public async Task<WorkshopCatalogPreviewDto?> GetPreviewAsync(
        ServerProfile profile,
        WorkshopPreset currentPreset,
        string workshopId,
        WorkshopCatalogSearchMode mode,
        CancellationToken cancellationToken = default)
    {
        var localItems = GetLocalItems(profile);
        var localItem = localItems.FirstOrDefault(item => string.Equals(item.WorkshopId, workshopId, StringComparison.OrdinalIgnoreCase));
        WorkshopCatalogItemDto? preview = localItem is null ? null : BuildLocalItem(profile, currentPreset, localItem);

        SteamWorkshopCollection? collection = null;
        if (mode is WorkshopCatalogSearchMode.Steam or WorkshopCatalogSearchMode.Both || preview is null)
        {
            collection = await steamWorkshopApiClient.GetCollectionAsync(workshopId, cancellationToken);
        }

        if (collection is not null)
        {
            return await BuildCollectionPreviewAsync(
                profile,
                currentPreset,
                collection,
                mode is WorkshopCatalogSearchMode.Local ? WorkshopCatalogItemSource.LocalAndSteam : WorkshopCatalogItemSource.Details,
                localItems,
                cancellationToken);
        }

        if (mode is WorkshopCatalogSearchMode.Steam or WorkshopCatalogSearchMode.Both || preview is null)
        {
            var detail = await steamWorkshopApiClient.GetDetailsAsync(workshopId, cancellationToken);
            if (detail is not null)
            {
                preview = MergeItems(preview, BuildDetailItem(profile, currentPreset, detail));
            }
        }

        if (preview is null)
        {
            return null;
        }

        var workshopItemsToAdd = currentPreset.WorkshopItemIds.Any(value => string.Equals(value, workshopId, StringComparison.OrdinalIgnoreCase))
            ? Array.Empty<string>()
            : [workshopId];
        var modIdsToAdd = preview.ModIds
            .Where(value => !currentPreset.EnabledModIds.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var mapFoldersToAdd = preview.MapFolders
            .Where(value => !currentPreset.MapFolders.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new WorkshopCatalogPreviewDto(preview, workshopItemsToAdd, modIdsToAdd, mapFoldersToAdd);
    }

    public async Task<SteamWorkshopImage?> GetImageAsync(
        ServerProfile profile,
        string workshopId,
        WorkshopCatalogItemSource source,
        CancellationToken cancellationToken = default)
    {
        var localItem = GetLocalItems(profile).FirstOrDefault(item => string.Equals(item.WorkshopId, workshopId, StringComparison.OrdinalIgnoreCase));
        if (source is WorkshopCatalogItemSource.Local or WorkshopCatalogItemSource.LocalAndSteam && localItem?.PreviewImagePath is not null)
        {
            return new SteamWorkshopImage(await File.ReadAllBytesAsync(localItem.PreviewImagePath, cancellationToken), ResolveContentType(localItem.PreviewImagePath));
        }

        if (source == WorkshopCatalogItemSource.Local && localItem?.PreviewImagePath is null)
        {
            return null;
        }

        var detail = await steamWorkshopApiClient.GetDetailsAsync(workshopId, cancellationToken);
        if (detail?.PreviewUrl is null)
        {
            return null;
        }

        return await steamWorkshopApiClient.DownloadImageAsync(detail.PreviewUrl, cancellationToken);
    }

    private IReadOnlyList<WorkshopCatalogItemDto> SearchLocal(
        ServerProfile profile,
        WorkshopPreset currentPreset,
        string query,
        int take,
        IReadOnlyList<LocalWorkshopCatalogItem> localItems) =>
        localItems
            .Where(item => Matches(item, query))
            .Select(item => BuildLocalItem(profile, currentPreset, item))
            .Take(take)
            .ToArray();

    private IReadOnlyList<LocalWorkshopCatalogItem> GetLocalItems(ServerProfile profile)
    {
        var installDirectory = profile.InstallDirectory.Trim();
        var cacheKey = $"local-workshop::{installDirectory}";
        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyList<LocalWorkshopCatalogItem>? cached) && cached is not null)
        {
            return cached;
        }

        var workshopRoot = ResolveWorkshopRoot(installDirectory);
        if (workshopRoot is null)
        {
            memoryCache.Set(cacheKey, Array.Empty<LocalWorkshopCatalogItem>(), TimeSpan.FromMinutes(2));
            return [];
        }

        var items = Directory.GetDirectories(workshopRoot)
            .Select(ReadLocalItem)
            .Where(item => item is not null)
            .Cast<LocalWorkshopCatalogItem>()
            .ToArray();

        memoryCache.Set(cacheKey, items, TimeSpan.FromMinutes(2));
        return items;
    }

    private static LocalWorkshopCatalogItem? ReadLocalItem(string itemDirectory)
    {
        var workshopId = Path.GetFileName(itemDirectory);
        if (string.IsNullOrWhiteSpace(workshopId))
        {
            return null;
        }

        var workshopMetadata = Directory.GetFiles(itemDirectory, "workshop.txt", SearchOption.AllDirectories)
            .Select(ReadWorkshopTxt)
            .FirstOrDefault(metadata => metadata is not null);
        var modInfos = Directory.GetFiles(itemDirectory, "mod.info", SearchOption.AllDirectories)
            .Select(ReadModInfo)
            .ToArray();
        var previewImagePath = Directory.GetFiles(itemDirectory, "preview.png", SearchOption.AllDirectories).FirstOrDefault();
        var modIds = modInfos.SelectMany(info => info.ModIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var mapFolders = modInfos.SelectMany(info => info.MapFolders).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var title = workshopMetadata?.Title
            ?? modInfos.Select(info => info.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? modIds.FirstOrDefault()
            ?? $"Workshop item {workshopId}";
        var description = workshopMetadata?.Description ?? string.Empty;

        return new LocalWorkshopCatalogItem(
            workshopId,
            title,
            description,
            previewImagePath,
            modIds,
            mapFolders);
    }

    private static LocalWorkshopTxtMetadata? ReadWorkshopTxt(string path)
    {
        string? title = null;
        string? description = null;

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("title=", StringComparison.OrdinalIgnoreCase))
            {
                title = line["title=".Length..].Trim();
            }
            else if (line.StartsWith("description=", StringComparison.OrdinalIgnoreCase))
            {
                description = line["description=".Length..].Trim().Replace("<LINE>", Environment.NewLine, StringComparison.OrdinalIgnoreCase);
            }
        }

        return title is null && description is null
            ? null
            : new LocalWorkshopTxtMetadata(title, description ?? string.Empty);
    }

    private static LocalWorkshopModInfo ReadModInfo(string path)
    {
        var modIds = new List<string>();
        var mapFolders = new List<string>();
        string? name = null;

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
            {
                modIds.Add(line["id=".Length..].Trim());
            }
            else if (line.StartsWith("map=", StringComparison.OrdinalIgnoreCase))
            {
                mapFolders.AddRange(line["map=".Length..]
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            }
            else if (line.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
            {
                name = line["name=".Length..].Trim();
            }
        }

        var mediaMapsDirectory = Path.Combine(Path.GetDirectoryName(path)!, "media", "maps");
        if (Directory.Exists(mediaMapsDirectory))
        {
            foreach (var mapDirectory in Directory.GetDirectories(mediaMapsDirectory))
            {
                mapFolders.Add(Path.GetFileName(mapDirectory));
            }
        }

        return new LocalWorkshopModInfo(name, modIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), mapFolders.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool Matches(LocalWorkshopCatalogItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return item.WorkshopId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               item.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               item.ModIds.Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               item.MapFolders.Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase));
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

    private static WorkshopCatalogItemDto BuildLocalItem(ServerProfile profile, WorkshopPreset currentPreset, LocalWorkshopCatalogItem item)
    {
        var source = item.PreviewImagePath is not null ? WorkshopCatalogItemSource.Local : WorkshopCatalogItemSource.Details;
        return new WorkshopCatalogItemDto(
            item.WorkshopId,
            item.Title,
            item.Description,
            BuildPreviewImageUrl(profile.ProfileId, item.WorkshopId, source),
            WorkshopCatalogItemSource.Local,
            true,
            WorkshopPresetMergeHelper.IsQueued(currentPreset, item.WorkshopId, item.ModIds, item.MapFolders),
            item.ModIds,
            item.MapFolders,
            WorkshopCatalogItemKind.Item,
            0,
            Array.Empty<string>());
    }

    private static WorkshopCatalogItemDto BuildSteamItem(ServerProfile profile, WorkshopPreset currentPreset, SteamWorkshopItem item) =>
        new(
            item.WorkshopId,
            item.Title,
            item.Description,
            BuildPreviewImageUrl(profile.ProfileId, item.WorkshopId, WorkshopCatalogItemSource.Steam, item.PreviewUrl),
            WorkshopCatalogItemSource.Steam,
            false,
            WorkshopPresetMergeHelper.IsQueued(currentPreset, item.WorkshopId, Array.Empty<string>(), Array.Empty<string>()),
            Array.Empty<string>(),
            Array.Empty<string>(),
            item.Kind,
            item.ChildWorkshopIds.Count,
            item.ChildWorkshopIds);

    private static WorkshopCatalogItemDto BuildDetailItem(ServerProfile profile, WorkshopPreset currentPreset, SteamWorkshopItem item)
    {
        var modIds = ExtractFromDescription(item.Description, "Mod ID");
        var mapFolders = ExtractFromDescription(item.Description, "Map Folder")
            .Concat(ExtractFromDescription(item.Description, "Map"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WorkshopCatalogItemDto(
            item.WorkshopId,
            item.Title,
            item.Description,
            BuildPreviewImageUrl(profile.ProfileId, item.WorkshopId, WorkshopCatalogItemSource.Steam, item.PreviewUrl),
            WorkshopCatalogItemSource.Details,
            false,
            WorkshopPresetMergeHelper.IsQueued(currentPreset, item.WorkshopId, modIds, mapFolders),
            modIds,
            mapFolders,
            item.Kind,
            item.ChildWorkshopIds.Count,
            item.ChildWorkshopIds);
    }

    private static WorkshopCatalogItemDto BuildCollectionItem(
        ServerProfile profile,
        WorkshopPreset currentPreset,
        SteamWorkshopCollection collection,
        IReadOnlyList<LocalWorkshopCatalogItem> localItems,
        WorkshopCatalogItemSource source)
    {
        var childWorkshopIds = collection.ChildWorkshopIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var localWorkshopIds = localItems
            .Select(item => item.WorkshopId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new WorkshopCatalogItemDto(
            collection.WorkshopId,
            collection.Title,
            collection.Description,
            BuildPreviewImageUrl(profile.ProfileId, collection.WorkshopId, source, collection.PreviewUrl),
            source,
            childWorkshopIds.Length > 0 && childWorkshopIds.All(localWorkshopIds.Contains),
            childWorkshopIds.Length > 0 && childWorkshopIds.All(id => currentPreset.WorkshopItemIds.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase))),
            Array.Empty<string>(),
            Array.Empty<string>(),
            WorkshopCatalogItemKind.Collection,
            childWorkshopIds.Length,
            childWorkshopIds);
    }

    private async Task<WorkshopCatalogPreviewDto> BuildCollectionPreviewAsync(
        ServerProfile profile,
        WorkshopPreset currentPreset,
        SteamWorkshopCollection collection,
        WorkshopCatalogItemSource source,
        IReadOnlyList<LocalWorkshopCatalogItem> localItems,
        CancellationToken cancellationToken)
    {
        var childWorkshopIds = collection.ChildWorkshopIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var localItemsById = localItems.ToDictionary(item => item.WorkshopId, StringComparer.OrdinalIgnoreCase);
        var detailItemsById = (await steamWorkshopApiClient.GetDetailsAsync(childWorkshopIds, cancellationToken))
            .ToDictionary(item => item.WorkshopId, StringComparer.OrdinalIgnoreCase);

        var childItems = new List<WorkshopCatalogItemDto>(childWorkshopIds.Length);
        foreach (var childWorkshopId in childWorkshopIds)
        {
            localItemsById.TryGetValue(childWorkshopId, out var localItem);
            detailItemsById.TryGetValue(childWorkshopId, out var detailItem);

            var merged = MergeItems(
                localItem is null ? null : BuildLocalItem(profile, currentPreset, localItem),
                detailItem is null ? BuildFallbackItem(childWorkshopId, currentPreset) : BuildDetailItem(profile, currentPreset, detailItem));
            childItems.Add(merged);
        }

        var aggregatedModIds = childItems
            .SelectMany(item => item.ModIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var aggregatedMapFolders = childItems
            .SelectMany(item => item.MapFolders)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var localWorkshopIds = localItemsById.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previewItem = new WorkshopCatalogItemDto(
            collection.WorkshopId,
            collection.Title,
            collection.Description,
            BuildPreviewImageUrl(profile.ProfileId, collection.WorkshopId, source, collection.PreviewUrl),
            source,
            childWorkshopIds.Length > 0 && childWorkshopIds.All(localWorkshopIds.Contains),
            childWorkshopIds.Length > 0 && childWorkshopIds.All(id => currentPreset.WorkshopItemIds.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase))),
            aggregatedModIds,
            aggregatedMapFolders,
            WorkshopCatalogItemKind.Collection,
            childWorkshopIds.Length,
            childWorkshopIds);
        var workshopItemsToAdd = childWorkshopIds
            .Where(id => !currentPreset.WorkshopItemIds.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var modIdsToAdd = aggregatedModIds
            .Where(value => !currentPreset.EnabledModIds.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var mapFoldersToAdd = aggregatedMapFolders
            .Where(value => !currentPreset.MapFolders.Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var collectionChildren = childItems
            .Select(item => new WorkshopCatalogPreviewChildDto(item.WorkshopId, item.Title, item.IsInstalledLocally, item.IsQueued))
            .ToArray();

        return new WorkshopCatalogPreviewDto(previewItem, workshopItemsToAdd, modIdsToAdd, mapFoldersToAdd, collectionChildren);
    }

    private static WorkshopCatalogItemDto MergeItems(WorkshopCatalogItemDto? primary, WorkshopCatalogItemDto secondary)
    {
        if (primary is null)
        {
            return secondary;
        }

        var source = (primary.Source, secondary.Source) switch
        {
            (WorkshopCatalogItemSource.Local, WorkshopCatalogItemSource.Steam) or
            (WorkshopCatalogItemSource.Steam, WorkshopCatalogItemSource.Local) or
            (WorkshopCatalogItemSource.Local, WorkshopCatalogItemSource.Details) or
            (WorkshopCatalogItemSource.Details, WorkshopCatalogItemSource.Local) => WorkshopCatalogItemSource.LocalAndSteam,
            _ => primary.Source,
        };
        var kind = primary.Kind is WorkshopCatalogItemKind.Collection || secondary.Kind is WorkshopCatalogItemKind.Collection
            ? WorkshopCatalogItemKind.Collection
            : WorkshopCatalogItemKind.Item;

        return new WorkshopCatalogItemDto(
            primary.WorkshopId,
            Choose(primary.Title, secondary.Title, $"Workshop item {primary.WorkshopId}"),
            Choose(primary.Description, secondary.Description, string.Empty),
            ChoosePreviewImageUrl(primary.PreviewImageUrl, secondary.PreviewImageUrl),
            source,
            primary.IsInstalledLocally || secondary.IsInstalledLocally,
            primary.IsQueued || secondary.IsQueued,
            primary.ModIds.Concat(secondary.ModIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            primary.MapFolders.Concat(secondary.MapFolders).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            kind,
            Math.Max(primary.CollectionItemCount, secondary.CollectionItemCount),
            primary.CollectionChildWorkshopIds?.Count > 0 ? primary.CollectionChildWorkshopIds : secondary.CollectionChildWorkshopIds);
    }

    private static WorkshopCatalogItemDto BuildFallbackItem(string workshopId, WorkshopPreset currentPreset) =>
        new(
            workshopId,
            $"Workshop item {workshopId}",
            string.Empty,
            null,
            WorkshopCatalogItemSource.Details,
            false,
            currentPreset.WorkshopItemIds.Any(value => string.Equals(value, workshopId, StringComparison.OrdinalIgnoreCase)),
            Array.Empty<string>(),
            Array.Empty<string>(),
            WorkshopCatalogItemKind.Item,
            0,
            Array.Empty<string>());

    private static string Choose(string current, string fallback, string defaultValue) =>
        string.IsNullOrWhiteSpace(current)
            ? string.IsNullOrWhiteSpace(fallback) ? defaultValue : fallback
            : current;

    private static bool MatchesFilter(WorkshopCatalogItemDto item, WorkshopCatalogSearchFilter filter) =>
        filter switch
        {
            WorkshopCatalogSearchFilter.Mods => item.Kind is WorkshopCatalogItemKind.Item,
            WorkshopCatalogSearchFilter.Collections => item.Kind is WorkshopCatalogItemKind.Collection,
            _ => true,
        };

    private static string? BuildPreviewImageUrl(
        string profileId,
        string workshopId,
        WorkshopCatalogItemSource source,
        string? remotePreviewUrl = null) =>
        string.IsNullOrWhiteSpace(remotePreviewUrl)
            ? BuildImageUrl(profileId, workshopId, source)
            : remotePreviewUrl;

    private static string? ChoosePreviewImageUrl(string? primary, string? secondary)
    {
        if (string.IsNullOrWhiteSpace(primary))
        {
            return secondary;
        }

        return IsInternalDetailsPreview(primary) && IsExternalPreview(secondary)
            ? secondary
            : primary;
    }

    private static bool IsInternalDetailsPreview(string value) =>
        value.Contains("/workshop-browser/items/", StringComparison.OrdinalIgnoreCase) &&
        value.Contains("source=Details", StringComparison.OrdinalIgnoreCase);

    private static bool IsExternalPreview(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string BuildImageUrl(string profileId, string workshopId, WorkshopCatalogItemSource source) =>
        $"/api/profiles/{Uri.EscapeDataString(profileId)}/workshop-browser/items/{Uri.EscapeDataString(workshopId)}/image?source={source}";

    private static string[] ExtractFromDescription(string description, string label)
    {
        var pattern = $@"{Regex.Escape(label)}\s*:\s*(?<value>[^\r\n]+)";
        return Regex.Matches(description, pattern, RegexOptions.IgnoreCase)
            .Select(match => match.Groups["value"].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveContentType(string path) =>
        ContentTypes.TryGetContentType(path, out var contentType)
            ? contentType
            : "application/octet-stream";

    private static string? NormalizeWorkshopLookup(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var urlMatch = WorkshopUrlRegex().Match(query);
        if (urlMatch.Success)
        {
            return urlMatch.Groups["id"].Value;
        }

        return DigitsOnlyRegex().IsMatch(query) ? query : null;
    }

    [GeneratedRegex(@"[?&]id=(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WorkshopUrlRegex();

    [GeneratedRegex(@"^\d+$", RegexOptions.Compiled)]
    private static partial Regex DigitsOnlyRegex();
}

internal sealed record LocalWorkshopCatalogItem(
    string WorkshopId,
    string Title,
    string Description,
    string? PreviewImagePath,
    IReadOnlyList<string> ModIds,
    IReadOnlyList<string> MapFolders);

internal sealed record LocalWorkshopTxtMetadata(string? Title, string Description);

internal sealed record LocalWorkshopModInfo(
    string? Name,
    IReadOnlyList<string> ModIds,
    IReadOnlyList<string> MapFolders);
