using Microsoft.Data.Sqlite;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetPortConflictMessageAsync_ReturnsMessageWhenAnotherProfileAlreadyUsesRequestedPort()
    {
        Directory.CreateDirectory(_tempRoot);
        var databasePath = Path.Combine(_tempRoot, "profile-store-conflict.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var store = new ProfileStore(dbContext);

        await store.UpsertAsync(CreateProfile("main-server", "Main Server", 16261));

        var conflict = await store.GetPortConflictMessageAsync(CreateProfile("second-server", "Second Server", 16262));

        Assert.NotNull(conflict);
        Assert.Contains("Main Server", conflict, StringComparison.Ordinal);
        Assert.Contains("16262", conflict, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPortConflictMessageAsync_ReturnsNullWhenRequestedPortSetIsAvailable()
    {
        Directory.CreateDirectory(_tempRoot);
        var databasePath = Path.Combine(_tempRoot, "profile-store-available.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var store = new ProfileStore(dbContext);

        await store.UpsertAsync(CreateProfile("main-server", "Main Server", 16261));

        var conflict = await store.GetPortConflictMessageAsync(CreateProfile("second-server", "Second Server", 16264));

        Assert.Null(conflict);
    }

    [Fact]
    public async Task UpsertAsync_CreatesManagedParentRootsButNotPerProfileLeafDirectories()
    {
        Directory.CreateDirectory(_tempRoot);
        var databasePath = Path.Combine(_tempRoot, "profile-store-directories.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var store = new ProfileStore(dbContext);
        var profile = CreateProfile("main-server", "Main Server", 16261);

        await store.UpsertAsync(profile);

        Assert.True(Directory.Exists(Path.GetDirectoryName(profile.InstallDirectory)!));
        Assert.True(Directory.Exists(Path.GetDirectoryName(profile.CacheDirectory)!));
        Assert.False(Directory.Exists(profile.InstallDirectory));
        Assert.False(Directory.Exists(profile.CacheDirectory));
    }

    [Fact]
    public async Task UpsertAsync_DoesNotCreateExternalDirectories()
    {
        Directory.CreateDirectory(_tempRoot);
        var databasePath = Path.Combine(_tempRoot, "profile-store-external-directories.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var store = new ProfileStore(dbContext);
        var profile = CreateProfile("imported-server", "Imported Server", 16270) with
        {
            InstallDirectory = Path.Combine(_tempRoot, "ExternalInstall", "imported-server"),
            CacheDirectory = Path.Combine(_tempRoot, "ExternalCache", "imported-server"),
        };

        await store.UpsertAsync(profile);

        Assert.False(Directory.Exists(profile.InstallDirectory));
        Assert.False(Directory.Exists(profile.CacheDirectory));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
                return;
            }
            catch (IOException)
            {
                SqliteConnection.ClearAllPools();
                Thread.Sleep(50);
            }
        }
    }

    private ServerProfile CreateProfile(string profileId, string displayName, int defaultPort) =>
        new()
        {
            ProfileId = profileId,
            DisplayName = displayName,
            ServerName = profileId,
            InstallDirectory = Path.Combine(_tempRoot, ServerProfileFactory.ManagedServersFolderName, ServerProfileFactory.InstallFolderName, profileId),
            CacheDirectory = Path.Combine(_tempRoot, ServerProfileFactory.ManagedServersFolderName, ServerProfileFactory.ProfilesFolderName, profileId),
            Branch = ProjectZomboidBranch.Unstable42,
            DefaultPort = defaultPort,
            UdpPort = defaultPort + 1,
            RconPort = defaultPort + 2,
            UseSteam = true,
            AdminUsername = "admin",
            AdminPassword = "change-me-before-first-run",
            PreferredMemoryInGigabytes = 6,
            StartWithHost = false,
            AutoRestartOnCrash = true,
            WorkshopPreset = WorkshopPreset.Empty,
            BackupPolicy = BackupPolicy.Default,
        };
}
