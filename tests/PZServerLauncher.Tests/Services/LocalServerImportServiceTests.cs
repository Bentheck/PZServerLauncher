using Microsoft.Data.Sqlite;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class LocalServerImportServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DiscoverAndImportAsync_UsesExistingServerConfig()
    {
        var cacheRoot = Path.Combine(_tempRoot, "Zomboid");
        var serverDirectory = Path.Combine(cacheRoot, "Server");
        Directory.CreateDirectory(serverDirectory);
        File.WriteAllText(
            Path.Combine(serverDirectory, "servertest.ini"),
            """
            DefaultPort=17261
            UDPPort=17262
            RCONPort=28015
            BindIP=127.0.0.1
            AdminUsername=admin
            WorkshopItems=1234567890
            Mods=ExampleMod
            Map=RavenCreek
            """);

        var installDirectory = Path.Combine(_tempRoot, "install");
        var itemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "1234567890", "mods", "ExampleMod");
        Directory.CreateDirectory(itemDirectory);
        File.WriteAllText(Path.Combine(itemDirectory, "mod.info"), "id=ExampleMod\nmap=RavenCreek");
        Directory.CreateDirectory(Path.Combine(itemDirectory, "media", "maps", "RavenCreek"));

        var databasePath = Path.Combine(_tempRoot, "import-tests.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var profileStore = new ProfileStore(dbContext);
        var service = new LocalServerImportService(
            profileStore,
            new WorkshopPresetScannerService(),
            cacheRoot,
            installDirectory,
            ProjectZomboidBranch.Unstable42);

        var candidates = await service.DiscoverAsync();
        var candidate = Assert.Single(candidates);
        Assert.Equal("servertest", candidate.ServerName);
        Assert.Equal(ProjectZomboidBranch.Unstable42, candidate.Branch);
        Assert.False(candidate.IsAlreadyImported);

        var imported = await service.ImportAsync(candidate.CandidateId);

        Assert.Equal("servertest", imported.ServerName);
        Assert.Equal(cacheRoot, imported.CacheDirectory);
        Assert.Equal(17261, imported.DefaultPort);
        Assert.Equal(17262, imported.UdpPort);
        Assert.Equal(28015, imported.RconPort);
        Assert.Equal("127.0.0.1", imported.BindIp);
        Assert.Equal(["1234567890"], imported.WorkshopPreset.WorkshopItemIds);
        Assert.Equal(["ExampleMod"], imported.WorkshopPreset.EnabledModIds);
        Assert.Equal(["RavenCreek"], imported.WorkshopPreset.MapFolders);
    }

    [Fact]
    public async Task DiscoverAsync_ParsesSemicolonDelimitedWorkshopPresetValuesFromIni()
    {
        var cacheRoot = Path.Combine(_tempRoot, "Zomboid");
        var serverDirectory = Path.Combine(cacheRoot, "Server");
        Directory.CreateDirectory(serverDirectory);
        File.WriteAllText(
            Path.Combine(serverDirectory, "servertest.ini"),
            """
            WorkshopItems= 1234567890 ; 2345678901
            Mods= ExampleMod ; AnotherMod
            Map= RavenCreek ; Louisville
            """);

        var databasePath = Path.Combine(_tempRoot, "import-trim-tests.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var profileStore = new ProfileStore(dbContext);
        var service = new LocalServerImportService(
            profileStore,
            new WorkshopPresetScannerService(),
            cacheRoot,
            null,
            ProjectZomboidBranch.Unstable42);

        var candidates = await service.DiscoverAsync();
        var candidate = Assert.Single(candidates);

        Assert.Equal(["1234567890", "2345678901"], candidate.WorkshopPreset.WorkshopItemIds);
        Assert.Equal(["ExampleMod", "AnotherMod"], candidate.WorkshopPreset.EnabledModIds);
        Assert.Equal(["RavenCreek", "Louisville"], candidate.WorkshopPreset.MapFolders);
    }

    [Fact]
    public async Task DiscoverAsync_PreservesStockMapNameContainingComma()
    {
        var cacheRoot = Path.Combine(_tempRoot, "Zomboid");
        var serverDirectory = Path.Combine(cacheRoot, "Server");
        Directory.CreateDirectory(serverDirectory);
        File.WriteAllText(
            Path.Combine(serverDirectory, "servertest.ini"),
            """
            Map=Muldraugh, KY
            """);

        var databasePath = Path.Combine(_tempRoot, "import-default-map-tests.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var profileStore = new ProfileStore(dbContext);
        var service = new LocalServerImportService(
            profileStore,
            new WorkshopPresetScannerService(),
            cacheRoot,
            null,
            ProjectZomboidBranch.Stable41);

        var candidates = await service.DiscoverAsync();
        var candidate = Assert.Single(candidates);

        Assert.Equal(["Muldraugh, KY"], candidate.WorkshopPreset.MapFolders);
    }

    [Fact]
    public async Task ImportAsync_WhenUdpPortIsMissing_UsesDefaultPortPlusOne()
    {
        var cacheRoot = Path.Combine(_tempRoot, "Zomboid");
        var serverDirectory = Path.Combine(cacheRoot, "Server");
        Directory.CreateDirectory(serverDirectory);
        File.WriteAllText(
            Path.Combine(serverDirectory, "servertest.ini"),
            """
            DefaultPort=19000
            RCONPort=29015
            """);

        var databasePath = Path.Combine(_tempRoot, "import-missing-udp-tests.db");
        await using var dbContext = TestDatabaseFactory.Create(databasePath);
        var profileStore = new ProfileStore(dbContext);
        var service = new LocalServerImportService(
            profileStore,
            new WorkshopPresetScannerService(),
            cacheRoot,
            null,
            ProjectZomboidBranch.Unstable42);

        var candidate = Assert.Single(await service.DiscoverAsync());
        var imported = await service.ImportAsync(candidate.CandidateId);

        Assert.Equal(19000, imported.DefaultPort);
        Assert.Equal(19001, imported.UdpPort);
        Assert.Equal(29015, imported.RconPort);
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
