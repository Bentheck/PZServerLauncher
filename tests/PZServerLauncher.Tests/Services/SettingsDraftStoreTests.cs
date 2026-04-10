using Microsoft.Data.Sqlite;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class SettingsDraftStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public SettingsDraftStoreTests()
    {
        _databasePath = Path.Combine(_tempRoot, "drafts.db");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task UpsertGetAndDeleteAsync_RoundTripsDraftByCompositeKey()
    {
        await using var dbContext = TestDatabaseFactory.Create(_databasePath);
        var store = new SettingsDraftStore(dbContext);
        var draft = new SettingsDraftDto(
            "profile-a",
            ProjectZomboidBranch.Stable41,
            "pz.settings.b41",
            1,
            "general",
            new Dictionary<string, string?>
            {
                ["b41.server.name"] = "servertest",
                ["b41.server.port"] = "16261",
            },
            "ABC123",
            true,
            DateTimeOffset.UtcNow);

        await store.UpsertAsync(draft);

        var loaded = await store.GetAsync("profile-a", ProjectZomboidBranch.Stable41, "pz.settings.b41", 1, "general");

        Assert.NotNull(loaded);
        Assert.Equal("servertest", loaded!.Values["b41.server.name"]);
        Assert.True(loaded.IsDirty);

        var deleted = await store.DeleteAsync("profile-a", ProjectZomboidBranch.Stable41, "pz.settings.b41", 1, "general");

        Assert.True(deleted);
        Assert.Null(await store.GetAsync("profile-a", ProjectZomboidBranch.Stable41, "pz.settings.b41", 1, "general"));
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
