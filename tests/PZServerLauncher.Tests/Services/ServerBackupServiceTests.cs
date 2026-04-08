using System.IO.Compression;
using Microsoft.Data.Sqlite;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class ServerBackupServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateBackupAndRestoreAsync_RoundTripsExpectedDirectories()
    {
        Directory.CreateDirectory(_tempRoot);
        var appPaths = new AppPaths(_tempRoot);
        var databasePath = Path.Combine(_tempRoot, "backup-tests.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var profileStore = new ProfileStore(dbContext);
        var auditStore = new AuditStore(dbContext);
        var planner = new ProjectZomboidServerPlanner();
        var backupService = new ServerBackupService(appPaths, profileStore, planner, auditStore);

        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "alpha",
            DisplayName = "Alpha",
            ServerName = "alpha",
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
            InstallDirectory = Path.Combine(_tempRoot, "install"),
        };
        await profileStore.UpsertAsync(profile);

        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(paths.ServerConfigDirectory);
        Directory.CreateDirectory(paths.WorldDirectory);
        Directory.CreateDirectory(Path.Combine(profile.CacheDirectory, "db"));
        await File.WriteAllTextAsync(paths.IniFilePath, "Public=true");
        await File.WriteAllTextAsync(Path.Combine(paths.WorldDirectory, "players.db"), "world-data");
        await File.WriteAllTextAsync(Path.Combine(profile.CacheDirectory, "db", "vehicles.db"), "db-data");

        var zipPath = await backupService.CreateBackupAsync(profile.ProfileId, BackupTrigger.Manual, CancellationToken.None);
        Assert.True(File.Exists(zipPath));

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            Assert.NotNull(archive.GetEntry("manifest.json"));
            Assert.NotNull(archive.GetEntry("profile.json"));
        }

        await File.WriteAllTextAsync(paths.IniFilePath, "Public=false");
        await File.WriteAllTextAsync(Path.Combine(paths.WorldDirectory, "players.db"), "mutated-world");

        await backupService.RestoreBackupAsync(profile.ProfileId, Path.GetFileName(zipPath), CancellationToken.None);

        Assert.Equal("Public=true", await File.ReadAllTextAsync(paths.IniFilePath));
        Assert.Equal("world-data", await File.ReadAllTextAsync(Path.Combine(paths.WorldDirectory, "players.db")));
        Assert.Equal("db-data", await File.ReadAllTextAsync(Path.Combine(profile.CacheDirectory, "db", "vehicles.db")));
        Assert.Equal(Path.GetFileName(zipPath), Assert.Single(backupService.ListBackups(profile.ProfileId)));
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
}
