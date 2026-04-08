using Microsoft.Data.Sqlite;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class NamedWorkshopPresetStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public NamedWorkshopPresetStoreTests()
    {
        _databasePath = Path.Combine(_tempRoot, "presets.db");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task UpsertListAndDeleteAsync_RoundTripsNamedPresetByProfile()
    {
        await using var dbContext = TestDatabaseFactory.Create(_databasePath);
        var store = new NamedWorkshopPresetStore(dbContext);
        var initialPreset = new WorkshopPreset
        {
            WorkshopItemIds = ["1234567890"],
            EnabledModIds = ["MyMod"],
            MapFolders = ["Muldraugh, KY"],
        };

        var saved = await store.UpsertAsync("profile-a", ProjectZomboidBranch.Stable41, "Mainline", initialPreset);

        Assert.Equal("Mainline", saved.Name);
        Assert.Single(saved.Preset.WorkshopItemIds);

        var updatedPreset = new WorkshopPreset
        {
            WorkshopItemIds = ["1234567890", "2222222222"],
            EnabledModIds = ["MyMod", "SecondMod"],
            MapFolders = ["Muldraugh, KY", "BedfordFalls"],
        };

        var updated = await store.UpsertAsync("profile-a", ProjectZomboidBranch.Stable41, "mainline", updatedPreset);
        await store.UpsertAsync("profile-b", ProjectZomboidBranch.Unstable42, "Mainline", initialPreset);
        var all = await store.ListAsync("profile-a");
        var otherProfilePresets = await store.ListAsync("profile-b");

        Assert.Single(all);
        Assert.Equal(saved.PresetId, updated.PresetId);
        Assert.Equal(2, all[0].Preset.WorkshopItemIds.Count);
        Assert.Equal(ProjectZomboidBranch.Stable41, all[0].Branch);
        Assert.Single(otherProfilePresets);
        Assert.Equal(ProjectZomboidBranch.Unstable42, otherProfilePresets[0].Branch);

        var deleted = await store.DeleteAsync("profile-a", updated.PresetId);

        Assert.True(deleted);
        Assert.Empty(await store.ListAsync("profile-a"));
        Assert.Single(await store.ListAsync("profile-b"));
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
