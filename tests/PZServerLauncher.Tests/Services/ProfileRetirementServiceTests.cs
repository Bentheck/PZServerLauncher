using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Infrastructure.Settings;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class ProfileRetirementServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _disposables = [];

    public ProfileRetirementServiceTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task UninstallServerAsync_RemovesManagedInstallButKeepsProfileAndData()
    {
        var harness = await CreateHarnessAsync();
        var profile = await CreateManagedProfileAsync(harness.ProfileStore);
        var installFile = Path.Combine(profile.InstallDirectory, "StartServer64.bat");
        var cacheFile = Path.Combine(profile.CacheDirectory, "Server", "servertest.ini");
        var backupDirectory = Path.Combine(harness.AppPaths.BackupsDirectory, profile.ProfileId);
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        Directory.CreateDirectory(backupDirectory);
        await File.WriteAllTextAsync(installFile, "echo test");
        await File.WriteAllTextAsync(cacheFile, "Public=true");
        await File.WriteAllTextAsync(Path.Combine(backupDirectory, "backup.zip"), "zip");

        var result = await harness.Service.UninstallServerAsync(profile.ProfileId, "test");

        Assert.True(result.RemovedManagedInstall);
        Assert.False(result.DeletedProfile);
        Assert.False(Directory.Exists(profile.InstallDirectory));
        Assert.True(Directory.Exists(profile.CacheDirectory));
        Assert.True(Directory.Exists(backupDirectory));
        Assert.NotNull(await harness.ProfileStore.GetAsync(profile.ProfileId));
    }

    [Fact]
    public async Task UninstallServerAsync_RemovesReadOnlyManagedInstallFiles()
    {
        var harness = await CreateHarnessAsync();
        var profile = await CreateManagedProfileAsync(harness.ProfileStore);
        var installFile = Path.Combine(profile.InstallDirectory, "StartServer64.bat");
        await File.WriteAllTextAsync(installFile, "echo test");
        File.SetAttributes(installFile, File.GetAttributes(installFile) | FileAttributes.ReadOnly);

        var result = await harness.Service.UninstallServerAsync(profile.ProfileId, "test");

        Assert.True(result.RemovedManagedInstall);
        Assert.False(Directory.Exists(profile.InstallDirectory));
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesLauncherArtifactsAndLeavesSingleDeleteAudit()
    {
        var harness = await CreateHarnessAsync();
        var profile = await CreateManagedProfileAsync(harness.ProfileStore);

        await File.WriteAllTextAsync(Path.Combine(profile.InstallDirectory, "StartServer64.bat"), "echo test");
        Directory.CreateDirectory(Path.Combine(profile.CacheDirectory, "Server"));
        await File.WriteAllTextAsync(Path.Combine(profile.CacheDirectory, "Server", "servertest.ini"), "Public=true");

        var backupsDirectory = Path.Combine(harness.AppPaths.BackupsDirectory, profile.ProfileId);
        var runtimeDirectory = harness.AppPaths.RuntimeProfileDirectory(profile.ProfileId);
        Directory.CreateDirectory(backupsDirectory);
        Directory.CreateDirectory(runtimeDirectory);
        await File.WriteAllTextAsync(Path.Combine(backupsDirectory, "backup.zip"), "zip");
        await File.WriteAllTextAsync(Path.Combine(runtimeDirectory, "launch.cmd"), "echo launch");
        harness.PersistentLogService.WriteProfileLine(profile.ProfileId, "profile log");
        harness.RuntimeStateStore.Update(new ServerRuntimeStatus(profile.ProfileId, ServerRuntimeState.Stopped, null, null, null, null, "latest"));

        harness.DbContext.SettingsDrafts.Add(new SettingsDraftEntity
        {
            ProfileId = profile.ProfileId,
            Branch = (int)profile.Branch,
            CatalogId = "sandbox",
            CatalogVersion = 1,
            PageId = "sandbox",
            ValuesJson = "{}",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        harness.DbContext.ModsMapsDrafts.Add(new ModsMapsDraftEntity
        {
            ProfileId = profile.ProfileId,
            Branch = (int)profile.Branch,
            WorkshopItemIdsJson = "[\"111111\"]",
            EditorMode = "Live",
            IsDirty = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        harness.DbContext.ModsMapsDraftModRows.Add(new ModsMapsDraftModRowEntity
        {
            ProfileId = profile.ProfileId,
            RowId = 1,
            ModName = "Active Mod",
            ModId = "ModA",
            WorkshopId = "111111",
            IsActive = true,
            SortOrder = 0,
        });
        harness.DbContext.ModsMapsDraftMapRows.Add(new ModsMapsDraftMapRowEntity
        {
            ProfileId = profile.ProfileId,
            RowId = 1,
            Title = "Map One",
            MapFolder = "MapOne",
            WorkshopId = "111111",
            IsActive = true,
            SortOrder = 0,
        });
        harness.DbContext.NamedWorkshopPresets.Add(new NamedWorkshopPresetEntity
        {
            PresetId = Guid.NewGuid(),
            ProfileId = profile.ProfileId,
            Branch = (int)profile.Branch,
            Name = "Preset",
            NormalizedName = "preset",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        harness.DbContext.OperationJobs.Add(new OperationJobEntity
        {
            JobId = Guid.NewGuid(),
            Kind = (int)OperationJobKind.Backup,
            Status = (int)OperationJobStatus.Succeeded,
            ProfileId = profile.ProfileId,
            Summary = "backup",
            ProgressPercent = 100,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        });
        harness.DbContext.AuditEntries.Add(new AuditEntryEntity
        {
            EntryId = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Action = "profile.updated",
            Subject = profile.ProfileId,
            ActorType = "test",
            Detail = "old audit",
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.DeleteProfileAsync(profile.ProfileId, "test");

        Assert.True(result.DeletedProfile);
        Assert.False(Directory.Exists(profile.InstallDirectory));
        Assert.False(Directory.Exists(profile.CacheDirectory));
        Assert.False(Directory.Exists(backupsDirectory));
        Assert.False(Directory.Exists(runtimeDirectory));
        Assert.Empty(harness.RuntimeStateStore.GetRecentLogs(profile.ProfileId));
        Assert.NotNull(await harness.DbContext.AuditEntries.SingleAsync(entry => entry.Subject == profile.ProfileId && entry.Action == "profile.deleted"));
        Assert.Equal(1, await harness.DbContext.AuditEntries.CountAsync(entry => entry.Subject == profile.ProfileId));
        Assert.False(await harness.DbContext.ServerProfiles.AnyAsync(entry => entry.ProfileId == profile.ProfileId));
        Assert.False(await harness.DbContext.SettingsDrafts.AnyAsync(entry => entry.ProfileId == profile.ProfileId));
        Assert.False(await harness.DbContext.ModsMapsDrafts.AnyAsync(entry => entry.ProfileId == profile.ProfileId));
        Assert.False(await harness.DbContext.ModsMapsDraftModRows.AnyAsync(entry => entry.ProfileId == profile.ProfileId));
        Assert.False(await harness.DbContext.ModsMapsDraftMapRows.AnyAsync(entry => entry.ProfileId == profile.ProfileId));
        Assert.False(await harness.DbContext.NamedWorkshopPresets.AnyAsync(entry => entry.ProfileId == profile.ProfileId));
        Assert.False(await harness.DbContext.OperationJobs.AnyAsync(entry => entry.ProfileId == profile.ProfileId));
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesReadOnlyManagedArtifactsAndLogs()
    {
        var harness = await CreateHarnessAsync();
        var profile = await CreateManagedProfileAsync(harness.ProfileStore);

        var installFile = Path.Combine(profile.InstallDirectory, "StartServer64.bat");
        var cacheFile = Path.Combine(profile.CacheDirectory, "Server", "servertest.ini");
        var backupDirectory = Path.Combine(harness.AppPaths.BackupsDirectory, profile.ProfileId);
        var runtimeDirectory = harness.AppPaths.RuntimeProfileDirectory(profile.ProfileId);
        var logPath = Path.Combine(harness.AppPaths.LogsDirectory, "profiles", $"{profile.ProfileId}.log");

        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        Directory.CreateDirectory(backupDirectory);
        Directory.CreateDirectory(runtimeDirectory);
        await File.WriteAllTextAsync(installFile, "echo test");
        await File.WriteAllTextAsync(cacheFile, "Public=true");
        await File.WriteAllTextAsync(Path.Combine(backupDirectory, "backup.zip"), "zip");
        await File.WriteAllTextAsync(Path.Combine(runtimeDirectory, "launch.cmd"), "echo launch");
        harness.PersistentLogService.WriteProfileLine(profile.ProfileId, "profile log");

        foreach (var path in new[]
                 {
                     installFile,
                     cacheFile,
                     Path.Combine(backupDirectory, "backup.zip"),
                     Path.Combine(runtimeDirectory, "launch.cmd"),
                     logPath,
                 })
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }

        var result = await harness.Service.DeleteProfileAsync(profile.ProfileId, "test");

        Assert.True(result.DeletedProfile);
        Assert.False(Directory.Exists(profile.InstallDirectory));
        Assert.False(Directory.Exists(profile.CacheDirectory));
        Assert.False(Directory.Exists(backupDirectory));
        Assert.False(Directory.Exists(runtimeDirectory));
        Assert.False(File.Exists(logPath));
    }

    [Fact]
    public async Task DeleteProfileAsync_PreservesExternalInstallAndCacheDirectories()
    {
        var harness = await CreateHarnessAsync();
        var profile = ServerProfileFactory.CreateStarterProfile("Imported Server", ServerProfileFactory.DefaultStarterPort, [], _tempRoot) with
        {
            InstallDirectory = Path.Combine(_tempRoot, "external", "install"),
            CacheDirectory = Path.Combine(_tempRoot, "external", "cache"),
        };
        profile = await harness.ProfileStore.UpsertAsync(profile);
        Directory.CreateDirectory(profile.InstallDirectory);
        Directory.CreateDirectory(profile.CacheDirectory);
        await File.WriteAllTextAsync(Path.Combine(profile.InstallDirectory, "keep.txt"), "install");
        await File.WriteAllTextAsync(Path.Combine(profile.CacheDirectory, "keep.txt"), "cache");

        var result = await harness.Service.DeleteProfileAsync(profile.ProfileId, "test");

        Assert.True(result.DeletedProfile);
        Assert.False(result.RemovedManagedInstall);
        Assert.False(result.RemovedManagedCache);
        Assert.True(Directory.Exists(profile.InstallDirectory));
        Assert.True(Directory.Exists(profile.CacheDirectory));
    }

    private async Task<TestHarness> CreateHarnessAsync()
    {
        var appPaths = new AppPaths(_tempRoot);
        var dbContext = TestDatabaseFactory.Create(appPaths.DatabasePath);
        var profileStore = new ProfileStore(dbContext);
        var jobStore = new JobStore(dbContext);
        var auditStore = new AuditStore(dbContext);
        var persistentLogService = new PersistentLogService(appPaths);
        var runtimeStateStore = new RuntimeStateStore(new ProjectZomboidLiveOperationsInterpreter(), persistentLogService);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var planner = new ProjectZomboidServerPlanner();
        var structuredSettingsService = new StructuredSettingsService(
            profileStore,
            new ConfigFileService(planner),
            new ProjectZomboidSettingsCatalogResolver(),
            new IniDocumentService(),
            new SandboxVarsDocumentService(),
            new WorkshopPresetScannerService());
        _disposables.Add(dbContext);
        _disposables.Add(serviceProvider);
        var supervisor = new ServerProcessSupervisor(
            appPaths,
            planner,
            structuredSettingsService,
            runtimeStateStore,
            new NullRuntimeEventPublisher(),
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ServerProcessSupervisor>.Instance);
        var service = new ProfileRetirementService(
            dbContext,
            profileStore,
            jobStore,
            auditStore,
            runtimeStateStore,
            persistentLogService,
            supervisor,
            appPaths);

        return await Task.FromResult(new TestHarness(
            appPaths,
            dbContext,
            profileStore,
            runtimeStateStore,
            persistentLogService,
            service));
    }

    private async Task<ServerProfile> CreateManagedProfileAsync(ProfileStore profileStore)
    {
        var profile = ServerProfileFactory.CreateStarterProfile("Main Server", ServerProfileFactory.DefaultStarterPort, [], _tempRoot);
        return await profileStore.UpsertAsync(profile);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
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

    private sealed record TestHarness(
        AppPaths AppPaths,
        ApplicationDbContext DbContext,
        ProfileStore ProfileStore,
        RuntimeStateStore RuntimeStateStore,
        PersistentLogService PersistentLogService,
        ProfileRetirementService Service);

    private sealed class NullRuntimeEventPublisher : IRuntimeEventPublisher
    {
        public Task PublishStatusChangedAsync(ServerRuntimeStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishJobChangedAsync(OperationJob job, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishLogLineAsync(string profileId, string line, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
