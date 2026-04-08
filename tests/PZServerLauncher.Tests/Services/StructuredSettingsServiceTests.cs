using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Infrastructure.Settings;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class StructuredSettingsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetPage_UsesIniBackedFieldsForGeneralAndNetwork_WhileKeepingLauncherFieldsProfileBacked()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-a",
            DisplayName = "Profile A",
            ServerName = "profile-server",
            InstallDirectory = Path.Combine(_tempRoot, "install"),
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
            BindIp = null,
            AdminUsername = "profile-admin",
            AdminPassword = "profile-secret",
            UdpPort = 16272,
            PreferredMemoryInGigabytes = 12,
            StartWithHost = true,
            AutoRestartOnCrash = false,
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, """
            PublicName=Alpha 42
            PublicDescription=Fresh apocalypse
            Public=true
            Open=false
            MaxPlayers=24
            PVP=false
            PauseEmpty=true
            GlobalChat=true
            ServerWelcomeMessage=Welcome survivor! <LINE> Stay alive.
            DefaultPort=16270
            RCONPort=27025
            BindIP=10.0.0.10
            Password=join-secret
            RCONPassword=rcon-secret
            AutoCreateUserInWhiteList=true
            DoLuaChecksum=false
            UPnP=false
            PingLimit=200
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "settings.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);

        var catalog = service.GetCatalog(profile);
        var generalPageDefinition = catalog.Pages.Single(page => page.PageId == ProfileWorkspacePageIds.General);
        var networkPageDefinition = catalog.Pages.Single(page => page.PageId == ProfileWorkspacePageIds.NetworkAndAdmin);

        Assert.All(generalPageDefinition.Sections.SelectMany(section => section.Fields), field => Assert.Equal(ConfigFileKind.Ini, field.SourceFile));
        Assert.All(networkPageDefinition.Sections.SelectMany(section => section.Fields), field => Assert.Equal(ConfigFileKind.Ini, field.SourceFile));

        var generalValues = service.GetPage(profile, ProfileWorkspacePageIds.General);
        var networkValues = service.GetPage(profile, ProfileWorkspacePageIds.NetworkAndAdmin);
        const string branchPrefix = "b42";

        Assert.Equal("Alpha 42", generalValues.Values[$"{branchPrefix}.server.public-name"]);
        Assert.Equal("Fresh apocalypse", generalValues.Values[$"{branchPrefix}.server.public-description"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.public"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.open"]);
        Assert.Equal("24", generalValues.Values[$"{branchPrefix}.server.max-players"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.pvp"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.pause-empty"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.global-chat"]);
        Assert.Contains(Environment.NewLine, generalValues.Values[$"{branchPrefix}.server.welcome-message"]);
        Assert.Equal("16270", generalValues.Values[$"{branchPrefix}.server.port"]);
        Assert.Equal(profile.UdpPort.ToString(), generalValues.Values[$"{branchPrefix}.server.udp-port"]);
        Assert.Equal("27025", generalValues.Values[$"{branchPrefix}.server.rcon-port"]);
        Assert.Equal(profile.PreferredMemoryInGigabytes.ToString(), generalValues.Values[$"{branchPrefix}.runtime.memory"]);
        Assert.Equal(profile.StartWithHost.ToString(), generalValues.Values[$"{branchPrefix}.runtime.start-with-host"]);
        Assert.Equal(profile.AutoRestartOnCrash.ToString(), generalValues.Values[$"{branchPrefix}.runtime.auto-restart"]);

        Assert.Equal("10.0.0.10", networkValues.Values[$"{branchPrefix}.network.bind-ip"]);
        Assert.Equal(string.Empty, networkValues.Values[$"{branchPrefix}.network.server-password"]);
        Assert.Equal(string.Empty, networkValues.Values[$"{branchPrefix}.network.rcon-password"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.auto-whitelist"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.do-lua-checksum"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.upnp"]);
        Assert.Equal("200", networkValues.Values[$"{branchPrefix}.network.ping-limit"]);
        Assert.Equal(profile.AdminUsername, networkValues.Values[$"{branchPrefix}.network.admin-user"]);
        Assert.Equal(string.Empty, networkValues.Values[$"{branchPrefix}.network.admin-password"]);
        Assert.False(generalValues.RequiresAdvancedFilesFallback);
        Assert.False(networkValues.RequiresAdvancedFilesFallback);
    }

    [Fact]
    public async Task SaveAsync_WritesIniFieldsAndKeepsLauncherRuntimeFieldsInProfile()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-b",
            DisplayName = "Profile B",
            ServerName = "profile-server",
            InstallDirectory = Path.Combine(_tempRoot, "install"),
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
            BindIp = "192.168.1.50",
            AdminUsername = "profile-admin",
            AdminPassword = "profile-secret",
            UdpPort = 16271,
            PreferredMemoryInGigabytes = 8,
            StartWithHost = false,
            AutoRestartOnCrash = true,
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, """
            PublicName=Old Name
            PublicDescription=Old description
            Public=true
            Open=true
            MaxPlayers=16
            PVP=true
            PauseEmpty=false
            GlobalChat=true
            ServerWelcomeMessage=Old welcome
            DefaultPort=16261
            RCONPort=27015
            BindIP=192.168.1.50
            Password=old-join
            RCONPassword=old-rcon
            AutoCreateUserInWhiteList=false
            DoLuaChecksum=true
            UPnP=true
            PingLimit=250
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "save.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);

        var generalResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.General, new Dictionary<string, string?>
        {
            ["b42.server.public-name"] = "The Final Broadcast",
            ["b42.server.public-description"] = "Hardcore Kentucky nights",
            ["b42.server.public"] = "false",
            ["b42.server.open"] = "false",
            ["b42.server.max-players"] = "32",
            ["b42.server.pvp"] = "true",
            ["b42.server.pause-empty"] = "true",
            ["b42.server.global-chat"] = "false",
            ["b42.server.welcome-message"] = "Welcome survivor!\nBring a can opener.",
            ["b42.server.port"] = "16270",
            ["b42.server.udp-port"] = "16273",
            ["b42.server.rcon-port"] = "27025",
            ["b42.runtime.memory"] = "14",
            ["b42.runtime.start-with-host"] = "true",
            ["b42.runtime.auto-restart"] = "false",
        });

        var profileAfterGeneral = await profileStore.GetAsync(profile.ProfileId);
        Assert.NotNull(profileAfterGeneral);

        var networkResult = await service.SaveAsync(profileAfterGeneral!, ProfileWorkspacePageIds.NetworkAndAdmin, new Dictionary<string, string?>
        {
            ["b42.network.bind-ip"] = "10.0.0.25",
            ["b42.network.server-password"] = "new-join-password",
            ["b42.network.rcon-password"] = "new-rcon-password",
            ["b42.network.auto-whitelist"] = "true",
            ["b42.network.do-lua-checksum"] = "false",
            ["b42.network.upnp"] = "false",
            ["b42.network.ping-limit"] = "180",
            ["b42.network.admin-user"] = "updated-admin",
            ["b42.network.admin-password"] = "updated-secret",
        });

        var updatedProfile = await profileStore.GetAsync(profile.ProfileId);
        var iniText = File.ReadAllText(paths.IniFilePath);

        Assert.NotNull(updatedProfile);
        Assert.True(generalResult.Validation.IsValid);
        Assert.True(networkResult.Validation.IsValid);

        Assert.Contains("PublicName=The Final Broadcast", iniText);
        Assert.Contains("PublicDescription=Hardcore Kentucky nights", iniText);
        Assert.Contains("Public=false", iniText);
        Assert.Contains("Open=false", iniText);
        Assert.Contains("MaxPlayers=32", iniText);
        Assert.Contains("PVP=true", iniText);
        Assert.Contains("PauseEmpty=true", iniText);
        Assert.Contains("GlobalChat=false", iniText);
        Assert.Contains("ServerWelcomeMessage=Welcome survivor! <LINE> Bring a can opener.", iniText);
        Assert.Contains("DefaultPort=16270", iniText);
        Assert.Contains("RCONPort=27025", iniText);
        Assert.Contains("BindIP=10.0.0.25", iniText);
        Assert.Contains("Password=new-join-password", iniText);
        Assert.Contains("RCONPassword=new-rcon-password", iniText);
        Assert.Contains("AutoCreateUserInWhiteList=true", iniText);
        Assert.Contains("DoLuaChecksum=false", iniText);
        Assert.Contains("UPnP=false", iniText);
        Assert.Contains("PingLimit=180", iniText);

        Assert.Equal(16270, updatedProfile!.DefaultPort);
        Assert.Equal(16273, updatedProfile.UdpPort);
        Assert.Equal(27025, updatedProfile.RconPort);
        Assert.Equal(14, updatedProfile.PreferredMemoryInGigabytes);
        Assert.True(updatedProfile.StartWithHost);
        Assert.False(updatedProfile.AutoRestartOnCrash);
        Assert.Equal("10.0.0.25", updatedProfile.BindIp);
        Assert.Equal("updated-admin", updatedProfile.AdminUsername);
        Assert.Equal("updated-secret", updatedProfile.AdminPassword);
    }

    [Fact]
    public async Task ModsAndMaps_PageReadsAndWritesIniBackedPresetValues()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-c",
            DisplayName = "Profile C",
            ServerName = "profile-server",
            InstallDirectory = Path.Combine(_tempRoot, "install"),
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
            WorkshopPreset = new WorkshopPreset
            {
                WorkshopItemIds = ["legacy-item"],
                EnabledModIds = ["legacy-mod"],
                MapFolders = ["legacy-map"],
            },
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, """
            WorkshopItems=1111111111;2222222222
            Mods=ExampleMod;AnotherMod
            Map=Muldraugh, KY;RavenCreek
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "mods.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);

        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.ModsAndMaps);

        Assert.False(valueSet.RequiresAdvancedFilesFallback);
        Assert.Equal("1111111111" + Environment.NewLine + "2222222222", valueSet.Values["b42.mods.workshop-items"]);
        Assert.Equal("ExampleMod" + Environment.NewLine + "AnotherMod", valueSet.Values["b42.mods.enabled-mods"]);
        Assert.Equal("Muldraugh, KY" + Environment.NewLine + "RavenCreek", valueSet.Values["b42.mods.map-folders"]);

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.ModsAndMaps, new Dictionary<string, string?>
        {
            ["b42.mods.workshop-items"] = "https://steamcommunity.com/sharedfiles/filedetails/?id=1234567890\n2345678901",
            ["b42.mods.enabled-mods"] = "ExampleMod\nAnotherMod",
            ["b42.mods.map-folders"] = "Muldraugh, KY\nRavenCreek",
        });

        var updatedProfile = await profileStore.GetAsync(profile.ProfileId);
        var iniText = File.ReadAllText(paths.IniFilePath);

        Assert.True(saveResult.Validation.IsValid);
        Assert.NotNull(updatedProfile);
        Assert.Contains("WorkshopItems=1234567890;2345678901", iniText);
        Assert.Contains("Mods=ExampleMod;AnotherMod", iniText);
        Assert.Contains("Map=Muldraugh, KY;RavenCreek", iniText);
        Assert.Equal(["1234567890", "2345678901"], updatedProfile!.WorkshopPreset.WorkshopItemIds);
        Assert.Equal(["ExampleMod", "AnotherMod"], updatedProfile.WorkshopPreset.EnabledModIds);
        Assert.Equal(["Muldraugh, KY", "RavenCreek"], updatedProfile.WorkshopPreset.MapFolders);
    }

    private static StructuredSettingsService CreateService(ProfileStore profileStore, ProjectZomboidServerPlanner planner) =>
        new(
            profileStore,
            new ConfigFileService(planner),
            new ProjectZomboidSettingsCatalogResolver(),
            new IniDocumentService(),
            new SandboxVarsDocumentService(),
            new WorkshopPresetScannerService());

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
