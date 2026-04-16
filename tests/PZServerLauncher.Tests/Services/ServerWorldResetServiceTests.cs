using System.Text.RegularExpressions;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Infrastructure.Settings;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class ServerWorldResetServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ResetWorldAsync_DeletesWorldCreatesBackupAndRandomizesResetMarkers()
    {
        Directory.CreateDirectory(_tempRoot);
        var appPaths = new AppPaths(Path.Combine(_tempRoot, "app"));
        var planner = new ProjectZomboidServerPlanner();
        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "world-reset.db"));
        var profileStore = new ProfileStore(dbContext);
        var auditStore = new AuditStore(dbContext);
        var backupService = new ServerBackupService(appPaths, profileStore, planner, auditStore);
        var service = new ServerWorldResetService(
            profileStore,
            planner,
            new ConfigFileService(planner),
            new IniDocumentService(),
            backupService,
            auditStore);

        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-world-reset",
            DisplayName = "Profile World Reset",
            ServerName = "profile-world-reset",
            InstallDirectory = Path.Combine(_tempRoot, "install"),
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
        };

        await profileStore.UpsertAsync(profile);

        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, """
            PublicName=Fresh Start
            ResetID=4
            Seed=OldSeed123456789
            """);

        Directory.CreateDirectory(paths.WorldDirectory);
        File.WriteAllText(Path.Combine(paths.WorldDirectory, "map_t.bin"), "old-world");

        var result = await service.ResetWorldAsync(profile.ProfileId, createBackupBeforeReset: true, CancellationToken.None);

        Assert.True(result.WorldDirectoryExisted);
        Assert.False(Directory.Exists(paths.WorldDirectory));
        Assert.NotNull(result.BackupFileName);
        Assert.True(result.UpdatedIni);
        Assert.Equal(5, result.ResetId);
        Assert.NotNull(result.Seed);
        Assert.Matches("^[A-Za-z0-9]{16}$", result.Seed!);

        var iniText = File.ReadAllText(paths.IniFilePath);
        Assert.Contains("ResetID=5", iniText);
        Assert.DoesNotContain("Seed=OldSeed123456789", iniText);
        Assert.Matches(new Regex(@"^Seed=[A-Za-z0-9]{16}$", RegexOptions.Multiline), iniText);

        var backupDirectory = Path.Combine(appPaths.BackupsDirectory, profile.ProfileId);
        Assert.Single(Directory.GetFiles(backupDirectory, "*.zip"));
    }

    [Fact]
    public async Task ResetWorldAsync_StillSucceedsWhenIniIsMissing()
    {
        Directory.CreateDirectory(_tempRoot);
        var appPaths = new AppPaths(Path.Combine(_tempRoot, "app-missing-ini"));
        var planner = new ProjectZomboidServerPlanner();
        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "world-reset-missing-ini.db"));
        var profileStore = new ProfileStore(dbContext);
        var auditStore = new AuditStore(dbContext);
        var backupService = new ServerBackupService(appPaths, profileStore, planner, auditStore);
        var service = new ServerWorldResetService(
            profileStore,
            planner,
            new ConfigFileService(planner),
            new IniDocumentService(),
            backupService,
            auditStore);

        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-world-reset-missing-ini",
            DisplayName = "Profile World Reset Missing Ini",
            ServerName = "profile-world-reset-missing-ini",
            InstallDirectory = Path.Combine(_tempRoot, "install-missing-ini"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-missing-ini"),
        };

        await profileStore.UpsertAsync(profile);

        var result = await service.ResetWorldAsync(profile.ProfileId, createBackupBeforeReset: false, CancellationToken.None);

        Assert.False(result.WorldDirectoryExisted);
        Assert.False(result.UpdatedIni);
        Assert.Null(result.ResetId);
        Assert.Null(result.Seed);
        Assert.Null(result.BackupFileName);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
