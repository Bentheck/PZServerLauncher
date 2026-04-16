using System.Net;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class WorkshopCatalogServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SearchAsync_LocalMode_ParsesWorkshopMetadataPreviewModsAndMaps()
    {
        var installDirectory = Path.Combine(_tempRoot, "install-local");
        var itemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "1234567890");
        var modDirectory = Path.Combine(itemDirectory, "mods", "ExampleMod");
        Directory.CreateDirectory(Path.Combine(modDirectory, "media", "maps", "RavenCreek"));
        File.WriteAllText(
            Path.Combine(itemDirectory, "workshop.txt"),
            """
            title=Expanded Helicopters
            description=Adds helicopters<LINE>and events
            """);
        File.WriteAllBytes(Path.Combine(itemDirectory, "preview.png"), [1, 2, 3, 4]);
        File.WriteAllText(
            Path.Combine(modDirectory, "mod.info"),
            """
            name=Example Mod
            id=ExampleMod
            map=RavenCreek
            """);

        var currentPreset = new WorkshopPreset
        {
            WorkshopItemIds = ["1234567890"],
            EnabledModIds = ["ExampleMod"],
            MapFolders = ["RavenCreek"],
        };
        var service = CreateService();

        var result = await service.SearchAsync(
            CreateProfile(installDirectory),
            currentPreset,
            new WorkshopCatalogSearchRequestDto("helicopters", WorkshopCatalogSearchMode.Local));

        var item = Assert.Single(result.Results);
        Assert.False(result.HasSteamWebApiKeyConfigured);
        Assert.Equal("Expanded Helicopters", item.Title);
        Assert.Contains("helicopters", item.Description, StringComparison.OrdinalIgnoreCase);
        Assert.True(item.IsInstalledLocally);
        Assert.True(item.IsQueued);
        Assert.Equal(WorkshopCatalogItemSource.Local, item.Source);
        Assert.Single(item.ModIds);
        Assert.Equal("ExampleMod", item.ModIds[0]);
        Assert.Single(item.MapFolders);
        Assert.Equal("RavenCreek", item.MapFolders[0]);
        Assert.NotNull(item.PreviewImageUrl);
    }

    [Fact]
    public async Task SearchAsync_BothModeWithoutKey_StillResolvesManualWorkshopIdFromSteamDetails()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
        {
            if (request.RequestUri?.ToString().Contains("GetCollectionDetails", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"response":{"publishedfiledetails":[{"publishedfileid":"3699503439","title":"Quick Restart","description":"Mod ID: QuickRestart\nMap Folder: RavenCreek","preview_url":"https://cdn.test/restart.png"}]}}
                    """),
            };
            return Task.FromResult(response);
        });
        var service = CreateService(handler);

        var result = await service.SearchAsync(
            CreateProfile(Path.Combine(_tempRoot, "install-steam")),
            WorkshopPreset.Empty,
            new WorkshopCatalogSearchRequestDto(
                "https://steamcommunity.com/sharedfiles/filedetails/?id=3699503439",
                WorkshopCatalogSearchMode.Both));

        var item = Assert.Single(result.Results);
        Assert.False(result.HasSteamWebApiKeyConfigured);
        Assert.Contains(result.Diagnostics, message => message.Contains("Steam search is unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Quick Restart", item.Title);
        Assert.Equal(WorkshopCatalogItemSource.Details, item.Source);
        Assert.Contains("QuickRestart", item.ModIds);
        Assert.Contains("RavenCreek", item.MapFolders);
        Assert.Equal("https://cdn.test/restart.png", item.PreviewImageUrl);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(handler.Requests, request => request.Uri.Contains("GetCollectionDetails", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(handler.Requests, request => request.Uri.Contains("GetPublishedFileDetails", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPreviewAsync_ComputesOnlyMissingValuesAgainstCurrentPreset()
    {
        var installDirectory = Path.Combine(_tempRoot, "install-preview");
        var itemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "1234567890");
        var modDirectory = Path.Combine(itemDirectory, "mods", "ExampleMod");
        Directory.CreateDirectory(Path.Combine(modDirectory, "media", "maps", "RavenCreek"));
        File.WriteAllText(Path.Combine(itemDirectory, "workshop.txt"), "title=Expanded Helicopters\ndescription=Adds helicopters");
        File.WriteAllText(
            Path.Combine(modDirectory, "mod.info"),
            """
            name=Example Mod
            id=ExampleMod
            map=RavenCreek
            """);

        var preset = new WorkshopPreset
        {
            WorkshopItemIds = ["1234567890"],
            EnabledModIds = ["ExistingMod"],
            MapFolders = ["RavenCreek"],
        };
        var handler = new RecordingHttpMessageHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = CreateService(handler);

        var preview = await service.GetPreviewAsync(
            CreateProfile(installDirectory),
            preset,
            "1234567890",
            WorkshopCatalogSearchMode.Local);

        Assert.NotNull(preview);
        Assert.Empty(preview!.WorkshopItemIdsToAdd);
        Assert.Equal(["ExampleMod"], preview.ModIdsToAdd);
        Assert.Empty(preview.MapFoldersToAdd);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_ManualCollectionIdReturnsCollectionResult()
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
                    {"response":{"publishedfiledetails":[{"publishedfileid":"5555555555","title":"Starter Collection","description":"Useful pack","preview_url":"https://cdn.test/starter.png"}]}}
                    """),
            });
        });
        var service = CreateService(handler);

        var result = await service.SearchAsync(
            CreateProfile(Path.Combine(_tempRoot, "install-collection-search")),
            WorkshopPreset.Empty,
            new WorkshopCatalogSearchRequestDto("https://steamcommunity.com/sharedfiles/filedetails/?id=5555555555", WorkshopCatalogSearchMode.Both));

        var item = Assert.Single(result.Results);
        Assert.Equal(WorkshopCatalogItemKind.Collection, item.Kind);
        Assert.Equal(2, item.CollectionItemCount);
        Assert.Equal(["1111111111", "2222222222"], item.CollectionChildWorkshopIds);
        Assert.Equal(WorkshopCatalogItemSource.Details, item.Source);
        Assert.Equal("https://cdn.test/starter.png", item.PreviewImageUrl);
    }

    [Fact]
    public async Task SearchAsync_CollectionsFilter_OnlyReturnsCollections()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
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
                    {"response":{"publishedfiledetails":[{"publishedfileid":"5555555555","title":"Starter Collection","description":"Useful pack","preview_url":"https://cdn.test/starter.png"}]}}
                    """),
            });
        });
        var service = CreateService(handler);

        var result = await service.SearchAsync(
            CreateProfile(Path.Combine(_tempRoot, "install-collection-filter")),
            WorkshopPreset.Empty,
            new WorkshopCatalogSearchRequestDto(
                "https://steamcommunity.com/sharedfiles/filedetails/?id=5555555555",
                WorkshopCatalogSearchMode.Both,
                SearchFilter: WorkshopCatalogSearchFilter.Collections));

        var item = Assert.Single(result.Results);
        Assert.Equal(WorkshopCatalogItemKind.Collection, item.Kind);
    }

    [Fact]
    public async Task SearchAsync_ModsFilter_ExcludesCollections()
    {
        var handler = new RecordingHttpMessageHandler((request, _, _) =>
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
                    {"response":{"publishedfiledetails":[{"publishedfileid":"5555555555","title":"Starter Collection","description":"Useful pack","preview_url":"https://cdn.test/starter.png"}]}}
                    """),
            });
        });
        var service = CreateService(handler);

        var result = await service.SearchAsync(
            CreateProfile(Path.Combine(_tempRoot, "install-mod-filter")),
            WorkshopPreset.Empty,
            new WorkshopCatalogSearchRequestDto(
                "https://steamcommunity.com/sharedfiles/filedetails/?id=5555555555",
                WorkshopCatalogSearchMode.Both,
                SearchFilter: WorkshopCatalogSearchFilter.Mods));

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task GetPreviewAsync_CollectionExpandsChildrenAndAggregatesModsAndMaps()
    {
        var handler = new RecordingHttpMessageHandler((request, body, _) =>
        {
            var requestUri = request.RequestUri?.ToString() ?? string.Empty;
            if (requestUri.Contains("GetCollectionDetails", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {"response":{"collectiondetails":[{"publishedfileid":"5555555555","result":1,"children":[{"publishedfileid":"1111111111"},{"publishedfileid":"2222222222"}]}]}}
                        """),
                });
            }

            if ((body ?? string.Empty).Contains("publishedfileids%5B0%5D=5555555555", StringComparison.OrdinalIgnoreCase) &&
                (body ?? string.Empty).Contains("itemcount=1", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {"response":{"publishedfiledetails":[{"publishedfileid":"5555555555","title":"Starter Collection","description":"Useful pack","preview_url":"https://cdn.test/starter.png"}]}}
                        """),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"response":{"publishedfiledetails":[
                        {"publishedfileid":"1111111111","title":"Quick Restart","description":"Mod ID: QuickRestart","preview_url":"https://cdn.test/restart.png"},
                        {"publishedfileid":"2222222222","title":"Raven Creek","description":"Map Folder: RavenCreek","preview_url":"https://cdn.test/raven.png"}
                    ]}}
                    """),
            });
        });
        var service = CreateService(handler);
        var preset = new WorkshopPreset
        {
            WorkshopItemIds = ["1111111111"],
        };

        var preview = await service.GetPreviewAsync(
            CreateProfile(Path.Combine(_tempRoot, "install-collection-preview")),
            preset,
            "5555555555",
            WorkshopCatalogSearchMode.Both);

        Assert.NotNull(preview);
        var collectionPreview = preview!;
        Assert.Equal(WorkshopCatalogItemKind.Collection, collectionPreview.Item.Kind);
        Assert.Equal(2, collectionPreview.Item.CollectionItemCount);
        Assert.Equal(["2222222222"], collectionPreview.WorkshopItemIdsToAdd);
        Assert.Equal(["QuickRestart"], collectionPreview.ModIdsToAdd);
        Assert.Equal(["RavenCreek"], collectionPreview.MapFoldersToAdd);
        var collectionChildren = collectionPreview.CollectionChildren ?? [];
        Assert.Equal(2, collectionChildren.Count);
        Assert.Contains(collectionChildren, child => child.WorkshopId == "1111111111" && child.IsQueued);
        Assert.Contains(collectionChildren, child => child.WorkshopId == "2222222222" && !child.IsQueued);
    }

    private WorkshopCatalogService CreateService(RecordingHttpMessageHandler? handler = null)
    {
        Directory.CreateDirectory(_tempRoot);
        var appPaths = new AppPaths(Path.Combine(_tempRoot, "app"));
        var dbContext = TestDatabaseFactory.Create(appPaths.DatabasePath);
        _ownedDbContexts.Add(dbContext);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _ownedCaches.Add(memoryCache);

        var httpClient = new HttpClient(handler ?? new RecordingHttpMessageHandler((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        _ownedHttpClients.Add(httpClient);

        var steamClient = new SteamWorkshopApiClient(new FakeHttpClientFactory(httpClient), memoryCache);
        var settingsStore = new WorkshopBrowserSettingsStore(dbContext, appPaths);
        return new WorkshopCatalogService(memoryCache, steamClient, settingsStore);
    }

    private static ServerProfile CreateProfile(string installDirectory) =>
        new()
        {
            ProfileId = "profile-a",
            DisplayName = "Profile A",
            ServerName = "profile-a",
            InstallDirectory = installDirectory,
            CacheDirectory = Path.Combine(installDirectory, "cache"),
            Branch = ProjectZomboidBranch.Unstable42,
        };

    private readonly List<ApplicationDbContext> _ownedDbContexts = [];
    private readonly List<IDisposable> _ownedCaches = [];
    private readonly List<HttpClient> _ownedHttpClients = [];

    public void Dispose()
    {
        foreach (var client in _ownedHttpClients)
        {
            client.Dispose();
        }

        foreach (var cache in _ownedCaches)
        {
            cache.Dispose();
        }

        foreach (var dbContext in _ownedDbContexts)
        {
            dbContext.Dispose();
        }

        SqliteConnection.ClearAllPools();

        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
        }
    }
}
