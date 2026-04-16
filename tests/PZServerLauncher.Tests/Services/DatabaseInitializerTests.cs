using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EnsureReadyAsync_PreservesExistingDataAndCreatesBackup()
    {
        var appPaths = new AppPaths(_tempRoot);
        var initializer = new DatabaseInitializer(appPaths, NullLogger<DatabaseInitializer>.Instance);

        await using (var dbContext = CreateContext(appPaths.DatabasePath))
        {
            await initializer.EnsureReadyAsync(dbContext);

            dbContext.HostSettings.Add(new HostSettingsEntity
            {
                Id = 1,
                LoopbackPort = 48239,
                StartHostWithWindows = true,
                RemoteAccessEnabled = false,
                RemoteBindAddress = "0.0.0.0",
                RemoteHttpsPort = 8443,
                OwnerIsConfigured = true,
                OwnerUserId = "owner-id",
                OwnerUserName = "owner",
                OwnerConfiguredAtUtc = DateTimeOffset.UtcNow,
            });

            dbContext.ServerProfiles.Add(new ServerProfileEntity
            {
                ProfileId = "profile-1",
                DisplayName = "Main Server",
                ServerName = "servertest",
                InstallDirectory = @"C:\Servers\PZ",
                CacheDirectory = @"C:\Users\Test\Zomboid",
                Branch = 0,
                DefaultPort = 16261,
                UdpPort = 16262,
                RconPort = 27015,
                UseSteam = true,
                PreferredMemoryInGigabytes = 4,
                StartWithHost = true,
                AutoRestartOnCrash = true,
                WorkshopItemIdsJson = "[\"123\"]",
                EnabledModIdsJson = "[\"ExampleMod\"]",
                MapFoldersJson = "[\"Muldraugh, KY\"]",
                ScheduledBackupsEnabled = true,
                ScheduledBackupRetentionCount = 10,
                PreUpdateBackupRetentionCount = 5,
                KeepManualBackupsForever = true,
                PreUpdateBackupEnabled = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });

            await dbContext.SaveChangesAsync();
        }

        await using (var reopened = CreateContext(appPaths.DatabasePath))
        {
            await initializer.EnsureReadyAsync(reopened);

            Assert.True(File.Exists(appPaths.DatabaseBackupPath));

            var hostSettings = await reopened.HostSettings.SingleAsync();
            var profile = await reopened.ServerProfiles.SingleAsync();

            Assert.Equal(48239, hostSettings.LoopbackPort);
            Assert.True(hostSettings.StartHostWithWindows);
            Assert.Equal("owner", hostSettings.OwnerUserName);
            Assert.Equal("Main Server", profile.DisplayName);
            Assert.True(profile.StartWithHost);
            Assert.Equal("[\"123\"]", profile.WorkshopItemIdsJson);
        }
    }

    private static ApplicationDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath};Cache=Shared")
            .Options;

        return new ApplicationDbContext(options);
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
            catch (IOException) when (attempt < 9)
            {
                SqliteConnection.ClearAllPools();
                Thread.Sleep(100);
            }
        }
    }
}
