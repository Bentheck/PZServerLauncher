using Microsoft.Data.Sqlite;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class ModsMapsDraftStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public ModsMapsDraftStoreTests()
    {
        _databasePath = Path.Combine(_tempRoot, "mods-maps-drafts.db");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task UpsertGetAndDeleteAsync_RoundTripsRowsAndDeduplicatesCaseInsensitiveValues()
    {
        await using var dbContext = TestDatabaseFactory.Create(_databasePath);
        var store = new ModsMapsDraftStore(dbContext);

        var draft = new ModsMapsDraftDto(
            "profile-a",
            ProjectZomboidBranch.Unstable42,
            ["111111", "111111", "222222"],
            [
                new ModsMapsModRowDto(3, "Mod B Title", "ModB", "222222", false, 1, ["CoreMod"], ["MapTwo"]),
                new ModsMapsModRowDto(2, "Mod A Title", "ModA", "111111", true, 0, [], ["MapOne"]),
                new ModsMapsModRowDto(4, "Mod A Duplicate", "moda", "111111", true, 2, [], []),
            ],
            [
                new ModsMapsMapRowDto(8, "Map One", "MapOne", "111111", true, 0),
                new ModsMapsMapRowDto(9, "Map One Duplicate", "mapone", "111111", false, 1),
                new ModsMapsMapRowDto(10, "Map Two", "MapTwo", "222222", false, 2),
            ],
            ModsMapsEditorMode.Live,
            true,
            DateTimeOffset.UtcNow);

        await store.UpsertAsync(draft);

        var loaded = await store.GetAsync("profile-a");

        Assert.NotNull(loaded);
        Assert.Equal(["111111", "222222"], loaded!.WorkshopItemIds);
        Assert.Equal(ModsMapsEditorMode.Live, loaded.EditorMode);
        Assert.Collection(
            loaded.ModRows,
            row =>
            {
                Assert.Equal(2, row.RowId);
                Assert.Equal("ModA", row.ModId);
                Assert.True(row.IsActive);
                Assert.Equal(0, row.SortOrder);
            },
            row =>
            {
                Assert.Equal(3, row.RowId);
                Assert.Equal("ModB", row.ModId);
                Assert.False(row.IsActive);
                Assert.Equal(["CoreMod"], row.DependencyModIds);
                Assert.Equal(["MapTwo"], row.MapFolders);
            });
        Assert.Collection(
            loaded.MapRows,
            row =>
            {
                Assert.Equal("MapOne", row.MapFolder);
                Assert.True(row.IsActive);
            },
            row =>
            {
                Assert.Equal("MapTwo", row.MapFolder);
                Assert.False(row.IsActive);
            });

        var deleted = await store.DeleteAsync("profile-a");

        Assert.True(deleted);
        Assert.Null(await store.GetAsync("profile-a"));
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
