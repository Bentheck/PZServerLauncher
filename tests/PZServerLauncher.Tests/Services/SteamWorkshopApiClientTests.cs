using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class SteamWorkshopApiClientTests
{
    [Fact]
    public async Task SearchAsync_UsesSteamWebApiKeyAndCachesResults()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"response":{"publishedfiledetails":[{"publishedfileid":"1234567890","title":"Expanded Helicopters","short_description":"Adds helicopters","preview_url":"https://cdn.test/heli.png","tags":[{"tag":"Mod"}]}]}}
                    """),
            };
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new SteamWorkshopApiClient(new FakeHttpClientFactory(httpClient), memoryCache);

        var first = await client.SearchAsync("steam-key-123", "helicopters", 10);
        var second = await client.SearchAsync("steam-key-123", "helicopters", 10);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Contains("key=steam-key-123", request.Uri, StringComparison.Ordinal);
        Assert.Contains("search_text=helicopters", request.Uri, StringComparison.Ordinal);
        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal("Expanded Helicopters", first[0].Title);
    }

    [Fact]
    public async Task GetDetailsAsync_PostsWorkshopIdWithoutApiKeyAndCachesResult()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"response":{"publishedfiledetails":[{"publishedfileid":"3699503439","title":"Quick Restart","description":"Mod ID: QuickRestart","preview_url":"https://cdn.test/restart.png"}]}}
                    """),
            };
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new SteamWorkshopApiClient(new FakeHttpClientFactory(httpClient), memoryCache);

        var first = await client.GetDetailsAsync("3699503439");
        var second = await client.GetDetailsAsync("3699503439");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Contains("itemcount=1", request.Body, StringComparison.Ordinal);
        Assert.Contains("publishedfileids%5B0%5D=3699503439", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("key=", request.Body, StringComparison.Ordinal);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Quick Restart", first!.Title);
    }

    [Fact]
    public async Task SearchCollectionsAsync_UsesCollectionFileTypeAndParsesChildren()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"response":{"publishedfiledetails":[{"publishedfileid":"5555555555","title":"Hardcore Collection","short_description":"A curated set","preview_url":"https://cdn.test/collection.png","children":[{"publishedfileid":"1111111111"},{"publishedfileid":"2222222222"}],"tags":[{"tag":"Collection"}]}]}}
                    """),
            };
            return Task.FromResult(response);
        });
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new SteamWorkshopApiClient(new FakeHttpClientFactory(httpClient), memoryCache);

        var result = await client.SearchCollectionsAsync("steam-key-123", "hardcore", 10);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("filetype=1", request.Uri, StringComparison.Ordinal);
        Assert.Contains("return_children=true", request.Uri, StringComparison.Ordinal);
        var collection = Assert.Single(result);
        Assert.Equal(WorkshopCatalogItemKind.Collection, collection.Kind);
        Assert.Equal(["1111111111", "2222222222"], collection.ChildWorkshopIds);
    }

    [Fact]
    public async Task GetCollectionAsync_LoadsRootDetailsAndChildWorkshopIds()
    {
        var handler = new RecordingHttpMessageHandler((request, body, _) =>
        {
            if (request.RequestUri?.ToString().Contains("GetCollectionDetails", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {"response":{"collectiondetails":[{"publishedfileid":"5555555555","result":1,"children":[{"publishedfileid":"1111111111"},{"publishedfileid":"2222222222"}]}]}}
                        """),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"response":{"publishedfiledetails":[{"publishedfileid":"5555555555","title":"Hardcore Collection","description":"A curated set","preview_url":"https://cdn.test/collection.png"}]}}
                    """),
            });
        });
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new SteamWorkshopApiClient(new FakeHttpClientFactory(httpClient), memoryCache);

        var collection = await client.GetCollectionAsync("5555555555");

        Assert.NotNull(collection);
        Assert.Equal("Hardcore Collection", collection!.Title);
        Assert.Equal(["1111111111", "2222222222"], collection.ChildWorkshopIds);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(handler.Requests, request => request.Uri.Contains("GetCollectionDetails", StringComparison.OrdinalIgnoreCase));
    }
}
