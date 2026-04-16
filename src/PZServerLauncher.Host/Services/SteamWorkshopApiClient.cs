using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using PZServerLauncher.Contracts.Profiles;

namespace PZServerLauncher.Host.Services;

public sealed class SteamWorkshopApiClient(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
{
    private const string QueryFilesEndpoint = "https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/";
    private const string DetailsEndpoint = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
    private const string CollectionDetailsEndpoint = "https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/";
    private const int ProjectZomboidAppId = 108600;

    public async Task<IReadOnlyList<SteamWorkshopItem>> SearchAsync(
        string apiKey,
        string query,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var cacheKey = $"steam-search::{normalizedQuery.ToLowerInvariant()}::{take}";
        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyList<SteamWorkshopItem>? cached) && cached is not null)
        {
            return cached;
        }

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["key"] = apiKey,
            ["appid"] = ProjectZomboidAppId.ToString(),
            ["creator_appid"] = ProjectZomboidAppId.ToString(),
            ["search_text"] = normalizedQuery,
            ["numperpage"] = Math.Clamp(take, 1, 50).ToString(),
            ["cursor"] = "*",
            ["query_type"] = "12",
            ["return_short_description"] = "true",
            ["return_previews"] = "true",
            ["return_tags"] = "true",
            ["strip_description_bbcode"] = "true",
        };

        var client = httpClientFactory.CreateClient(nameof(SteamWorkshopApiClient));
        using var response = await SendQueryFilesAsync(client, parameters, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var items = ParseQueryItems(document, WorkshopCatalogItemKind.Item);
        memoryCache.Set(cacheKey, items, TimeSpan.FromMinutes(10));
        return items;
    }

    public async Task<IReadOnlyList<SteamWorkshopItem>> SearchCollectionsAsync(
        string apiKey,
        string query,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var cacheKey = $"steam-collection-search::{normalizedQuery.ToLowerInvariant()}::{take}";
        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyList<SteamWorkshopItem>? cached) && cached is not null)
        {
            return cached;
        }

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["key"] = apiKey,
            ["appid"] = ProjectZomboidAppId.ToString(),
            ["creator_appid"] = ProjectZomboidAppId.ToString(),
            ["search_text"] = normalizedQuery,
            ["numperpage"] = Math.Clamp(take, 1, 50).ToString(),
            ["cursor"] = "*",
            ["query_type"] = "12",
            ["filetype"] = "1",
            ["return_short_description"] = "true",
            ["return_previews"] = "true",
            ["return_tags"] = "true",
            ["return_children"] = "true",
            ["strip_description_bbcode"] = "true",
        };

        var client = httpClientFactory.CreateClient(nameof(SteamWorkshopApiClient));
        using var response = await SendQueryFilesAsync(client, parameters, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var items = ParseQueryItems(document, WorkshopCatalogItemKind.Collection);
        memoryCache.Set(cacheKey, items, TimeSpan.FromMinutes(10));
        return items;
    }

    public async Task<SteamWorkshopItem?> GetDetailsAsync(string workshopId, CancellationToken cancellationToken = default)
    {
        var items = await GetDetailsAsync([workshopId], cancellationToken);
        return items.FirstOrDefault();
    }

    public async Task<IReadOnlyList<SteamWorkshopItem>> GetDetailsAsync(
        IReadOnlyList<string> workshopIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedIds = workshopIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedIds.Length == 0)
        {
            return [];
        }

        var results = new Dictionary<string, SteamWorkshopItem>(StringComparer.OrdinalIgnoreCase);
        var missingIds = new List<string>();

        foreach (var normalizedId in normalizedIds)
        {
            var cacheKey = $"steam-detail::{normalizedId}";
            if (memoryCache.TryGetValue(cacheKey, out SteamWorkshopItem? cached) && cached is not null)
            {
                results[normalizedId] = cached;
            }
            else
            {
                missingIds.Add(normalizedId);
            }
        }

        if (missingIds.Count > 0)
        {
            var payload = new Dictionary<string, string>
            {
                ["itemcount"] = missingIds.Count.ToString(),
            };
            for (var index = 0; index < missingIds.Count; index++)
            {
                payload[$"publishedfileids[{index}]"] = missingIds[index];
            }

            var body = new FormUrlEncodedContent(payload);
            var client = httpClientFactory.CreateClient(nameof(SteamWorkshopApiClient));
            using var response = await client.PostAsync(DetailsEndpoint, body, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            foreach (var item in ParseDetailItems(document))
            {
                results[item.WorkshopId] = item;
                memoryCache.Set($"steam-detail::{item.WorkshopId}", item, TimeSpan.FromMinutes(30));
            }
        }

        return normalizedIds
            .Select(id => results.GetValueOrDefault(id))
            .Where(item => item is not null)
            .Cast<SteamWorkshopItem>()
            .ToArray();
    }

    public async Task<SteamWorkshopCollection?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        var normalizedId = collectionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return null;
        }

        var cacheKey = $"steam-collection::{normalizedId}";
        if (memoryCache.TryGetValue(cacheKey, out SteamWorkshopCollection? cached) && cached is not null)
        {
            return cached;
        }

        var childWorkshopIds = await GetCollectionChildWorkshopIdsAsync(normalizedId, cancellationToken);
        if (childWorkshopIds is null)
        {
            return null;
        }

        var detail = await GetDetailsAsync(normalizedId, cancellationToken);
        var collection = new SteamWorkshopCollection(
            normalizedId,
            detail?.Title ?? $"Collection {normalizedId}",
            detail?.Description ?? string.Empty,
            detail?.PreviewUrl,
            detail?.Tags ?? [],
            childWorkshopIds);
        memoryCache.Set(cacheKey, collection, TimeSpan.FromMinutes(30));
        return collection;
    }

    public async Task<SteamWorkshopImage?> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        var normalizedUrl = imageUrl.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return null;
        }

        var cacheKey = $"steam-image::{normalizedUrl}";
        if (memoryCache.TryGetValue(cacheKey, out SteamWorkshopImage? cached) && cached is not null)
        {
            return cached;
        }

        var client = httpClientFactory.CreateClient(nameof(SteamWorkshopApiClient));
        using var request = new HttpRequestMessage(HttpMethod.Get, normalizedUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PZServerLauncher", "1.0"));
        request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var image = new SteamWorkshopImage(bytes, contentType);
        memoryCache.Set(cacheKey, image, TimeSpan.FromHours(1));
        return image;
    }

    private static async Task<HttpResponseMessage> SendQueryFilesAsync(
        HttpClient client,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken)
    {
        var getUrl = QueryHelpers.AddQueryString(QueryFilesEndpoint, parameters);
        var getResponse = await client.GetAsync(getUrl, cancellationToken);
        if (getResponse.IsSuccessStatusCode)
        {
            return getResponse;
        }

        getResponse.Dispose();
        var postBody = new FormUrlEncodedContent(parameters
            .Where(pair => pair.Value is not null)
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!)));
        return await client.PostAsync(QueryFilesEndpoint, postBody, cancellationToken);
    }

    private async Task<IReadOnlyList<string>?> GetCollectionChildWorkshopIdsAsync(
        string collectionId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"steam-collection-children::{collectionId}";
        if (memoryCache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["collectioncount"] = "1",
            ["publishedfileids[0]"] = collectionId,
        });

        var client = httpClientFactory.CreateClient(nameof(SteamWorkshopApiClient));
        using var response = await client.PostAsync(CollectionDetailsEndpoint, body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var childWorkshopIds = ParseCollectionChildWorkshopIds(document);
        if (childWorkshopIds is not null)
        {
            memoryCache.Set(cacheKey, childWorkshopIds, TimeSpan.FromMinutes(30));
        }

        return childWorkshopIds;
    }

    private static IReadOnlyList<SteamWorkshopItem> ParseQueryItems(JsonDocument document, WorkshopCatalogItemKind kind)
    {
        if (!document.RootElement.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("publishedfiledetails", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item => ParseItem(item, kind))
            .Where(item => item is not null)
            .Cast<SteamWorkshopItem>()
            .ToArray();
    }

    private static IReadOnlyList<SteamWorkshopItem> ParseDetailItems(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("publishedfiledetails", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item => ParseItem(item, null))
            .Where(item => item is not null)
            .Cast<SteamWorkshopItem>()
            .ToArray();
    }

    private static IReadOnlyList<string>? ParseCollectionChildWorkshopIds(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("collectiondetails", out var details) ||
            details.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var detail in details.EnumerateArray())
        {
            if (detail.TryGetProperty("result", out var result) &&
                result.ValueKind == JsonValueKind.Number &&
                result.GetInt32() != 1)
            {
                return null;
            }

            if (!detail.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return children.EnumerateArray()
                .Select(child => GetString(child, "publishedfileid"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return null;
    }

    private static SteamWorkshopItem? ParseItem(JsonElement element, WorkshopCatalogItemKind? explicitKind)
    {
        var workshopId = GetString(element, "publishedfileid");
        if (string.IsNullOrWhiteSpace(workshopId))
        {
            return null;
        }

        var title = GetString(element, "title") ?? $"Workshop item {workshopId}";
        var description = GetString(element, "short_description")
            ?? GetString(element, "file_description")
            ?? GetString(element, "description")
            ?? string.Empty;
        var previewUrl = GetString(element, "preview_url");
        var tags = element.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array
            ? tagsElement.EnumerateArray()
                .Select(tag => GetString(tag, "tag"))
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Cast<string>()
                .ToArray()
            : [];
        var childWorkshopIds = element.TryGetProperty("children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array
            ? childrenElement.EnumerateArray()
                .Select(child => GetString(child, "publishedfileid"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        var kind = explicitKind ?? (childWorkshopIds.Length > 0 ? WorkshopCatalogItemKind.Collection : WorkshopCatalogItemKind.Item);

        return new SteamWorkshopItem(workshopId, title, description, previewUrl, tags, kind, childWorkshopIds);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

public sealed record SteamWorkshopItem(
    string WorkshopId,
    string Title,
    string Description,
    string? PreviewUrl,
    IReadOnlyList<string> Tags,
    WorkshopCatalogItemKind Kind,
    IReadOnlyList<string> ChildWorkshopIds);

public sealed record SteamWorkshopImage(byte[] Content, string ContentType);

public sealed record SteamWorkshopCollection(
    string WorkshopId,
    string Title,
    string Description,
    string? PreviewUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> ChildWorkshopIds);
