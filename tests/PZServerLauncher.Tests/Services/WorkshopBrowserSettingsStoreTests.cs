using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class WorkshopBrowserSettingsStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SetGetAndRemoveAsync_RoundTripsProtectedSteamWebApiKey()
    {
        var appPaths = new AppPaths(_tempRoot);
        await using var dbContext = TestDatabaseFactory.Create(appPaths.DatabasePath);
        var store = new WorkshopBrowserSettingsStore(dbContext, appPaths);

        var initial = await store.GetAsync();

        Assert.False(initial.HasSteamWebApiKeyConfigured);

        var configured = await store.SetSteamWebApiKeyAsync("steam-key-123");
        var entity = await dbContext.HostSettings.SingleAsync();
        var loadedKey = await store.GetSteamWebApiKeyAsync();

        Assert.True(configured.HasSteamWebApiKeyConfigured);
        Assert.NotNull(entity.ProtectedSteamWebApiKey);
        Assert.NotEqual("steam-key-123", entity.ProtectedSteamWebApiKey);
        Assert.Equal("steam-key-123", loadedKey);

        var removed = await store.RemoveSteamWebApiKeyAsync();

        Assert.False(removed.HasSteamWebApiKeyConfigured);
        Assert.Null(await store.GetSteamWebApiKeyAsync());
    }

    public void Dispose()
    {
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
