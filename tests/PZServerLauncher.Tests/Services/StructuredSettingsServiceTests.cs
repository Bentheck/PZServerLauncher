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
            SpawnItems=Base.BaseballBat,Base.WaterBottleFull
            HoursForLootRespawn=6
            MaxItemsForLootRespawn=3
            ConstructionPreventsLootRespawn=false
            PlayerRespawnWithSelf=true
            PlayerRespawnWithOther=false
            HoursForWorldItemRemoval=24.0
            WorldItemRemovalList=Base.TinCanEmpty,Base.PopBottleEmpty
            SleepAllowed=true
            SleepNeeded=false
            NoFire=true
            AnnounceDeath=false
            DropOffWhiteListAfterDeath=true
            AllowDestructionBySledgehammer=false
            PlayerSafehouse=true
            AdminSafehouse=false
            SafehouseAllowTrepass=true
            SafehouseAllowFire=false
            SafehouseAllowLoot=false
            SafehouseAllowRespawn=true
            SafehouseDaySurvivedToClaim=14
            SafeHouseRemovalTime=240
            Faction=true
            FactionDaySurvivedToCreate=3
            FactionPlayersRequiredForTag=4
            AllowTradeUI=false
            SteamVAC=false
            KickFastPlayers=true
            DisplayUserName=false
            ShowFirstAndLastName=true
            SafetySystem=false
            SafetyToggleTimer=30
            SafetyCooldownTimer=45
            VoiceEnable=false
            Voice3D=false
            VoiceMinDistance=12
            VoiceMaxDistance=80
            DefaultPort=16270
            RCONPort=27025
            BindIP=10.0.0.10
            Password=join-secret
            RCONPassword=rcon-secret
            AutoCreateUserInWhiteList=true
            DoLuaChecksum=false
            UPnP=false
            PingLimit=200
            SteamVAC=true
            KickFastPlayers=false
            DenyLoginOnOverloadedServer=false
            PlayerSaveOnDamage=false
            DisplayUserName=true
            ShowFirstAndLastName=false
            MouseOverToSeeDisplayName=true
            HidePlayersBehindYou=true
            PlayerBumpPlayer=false
            SafetySystem=true
            ShowSafety=false
            SafetyToggleTimer=3
            SafetyCooldownTimer=15
            MaxAccountsPerUser=2
            AllowNonAsciiUsername=true
            VoiceEnable=true
            Voice3D=true
            VoiceMinDistance=10
            VoiceMaxDistance=55
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
        Assert.Equal($"Base.BaseballBat{Environment.NewLine}Base.WaterBottleFull", generalValues.Values[$"{branchPrefix}.server.spawn-items"]);
        Assert.Equal("6", generalValues.Values[$"{branchPrefix}.server.loot-respawn-hours"]);
        Assert.Equal("3", generalValues.Values[$"{branchPrefix}.server.loot-respawn-max-items"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.construction-prevents-loot-respawn"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.respawn-with-self"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.respawn-with-other"]);
        Assert.Equal("24.0", generalValues.Values[$"{branchPrefix}.server.world-item-removal-hours"]);
        Assert.Equal($"Base.TinCanEmpty{Environment.NewLine}Base.PopBottleEmpty", generalValues.Values[$"{branchPrefix}.server.world-item-removal-list"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.sleep-allowed"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.sleep-needed"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.no-fire"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.announce-death"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.drop-whitelist-on-death"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.allow-sledgehammer-destruction"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.player-safehouse"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.admin-safehouse"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.safehouse-allow-trespass"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.safehouse-allow-fire"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.safehouse-allow-loot"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.safehouse-allow-respawn"]);
        Assert.Equal("14", generalValues.Values[$"{branchPrefix}.server.safehouse-days-to-claim"]);
        Assert.Equal("240", generalValues.Values[$"{branchPrefix}.server.safehouse-removal-hours"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.faction-enabled"]);
        Assert.Equal("3", generalValues.Values[$"{branchPrefix}.server.faction-days-to-create"]);
        Assert.Equal("4", generalValues.Values[$"{branchPrefix}.server.faction-players-for-tag"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.allow-trade-ui"]);
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
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.steam-vac"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.kick-fast-players"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.deny-login-overloaded"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.player-save-on-damage"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.display-user-name"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.show-first-last-name"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.mouse-over-display-name"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.hide-players-behind-you"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.player-bump-player"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.safety-system"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.show-safety"]);
        Assert.Equal("3", networkValues.Values[$"{branchPrefix}.network.safety-toggle-timer"]);
        Assert.Equal("15", networkValues.Values[$"{branchPrefix}.network.safety-cooldown-timer"]);
        Assert.Equal("2", networkValues.Values[$"{branchPrefix}.network.max-accounts-per-user"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.allow-non-ascii-username"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.voice-enabled"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.voice-3d"]);
        Assert.Equal("10", networkValues.Values[$"{branchPrefix}.network.voice-min-distance"]);
        Assert.Equal("55", networkValues.Values[$"{branchPrefix}.network.voice-max-distance"]);
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
            SpawnItems=Base.Bag_Schoolbag
            HoursForLootRespawn=0
            MaxItemsForLootRespawn=4
            ConstructionPreventsLootRespawn=true
            PlayerRespawnWithSelf=false
            PlayerRespawnWithOther=true
            HoursForWorldItemRemoval=0.0
            WorldItemRemovalList=Base.EmptyPetrolCan
            SleepAllowed=false
            SleepNeeded=false
            NoFire=false
            AnnounceDeath=true
            DropOffWhiteListAfterDeath=false
            AllowDestructionBySledgehammer=true
            PlayerSafehouse=true
            AdminSafehouse=false
            SafehouseAllowTrepass=true
            SafehouseAllowFire=true
            SafehouseAllowLoot=true
            SafehouseAllowRespawn=false
            SafehouseDaySurvivedToClaim=0
            SafeHouseRemovalTime=144
            Faction=true
            FactionDaySurvivedToCreate=0
            FactionPlayersRequiredForTag=1
            AllowTradeUI=true
            DefaultPort=16261
            RCONPort=27015
            BindIP=192.168.1.50
            Password=old-join
            RCONPassword=old-rcon
            AutoCreateUserInWhiteList=false
            DoLuaChecksum=true
            UPnP=true
            PingLimit=250
            SteamVAC=true
            KickFastPlayers=false
            DenyLoginOnOverloadedServer=true
            PlayerSaveOnDamage=true
            DisplayUserName=true
            ShowFirstAndLastName=false
            MouseOverToSeeDisplayName=false
            HidePlayersBehindYou=false
            PlayerBumpPlayer=true
            SafetySystem=true
            ShowSafety=true
            SafetyToggleTimer=2
            SafetyCooldownTimer=10
            MaxAccountsPerUser=0
            AllowNonAsciiUsername=false
            VoiceEnable=true
            Voice3D=true
            VoiceMinDistance=8
            VoiceMaxDistance=45
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
            ["b42.server.spawn-items"] = "Base.BaseballBat\nBase.WaterBottleFull",
            ["b42.server.loot-respawn-hours"] = "12",
            ["b42.server.loot-respawn-max-items"] = "2",
            ["b42.server.construction-prevents-loot-respawn"] = "false",
            ["b42.server.respawn-with-self"] = "true",
            ["b42.server.respawn-with-other"] = "false",
            ["b42.server.world-item-removal-hours"] = "36.5",
            ["b42.server.world-item-removal-list"] = "Base.TinCanEmpty\nBase.PopBottleEmpty",
            ["b42.server.sleep-allowed"] = "true",
            ["b42.server.sleep-needed"] = "true",
            ["b42.server.no-fire"] = "true",
            ["b42.server.announce-death"] = "false",
            ["b42.server.drop-whitelist-on-death"] = "true",
            ["b42.server.allow-sledgehammer-destruction"] = "false",
            ["b42.server.player-safehouse"] = "true",
            ["b42.server.admin-safehouse"] = "true",
            ["b42.server.safehouse-allow-trespass"] = "false",
            ["b42.server.safehouse-allow-fire"] = "false",
            ["b42.server.safehouse-allow-loot"] = "false",
            ["b42.server.safehouse-allow-respawn"] = "true",
            ["b42.server.safehouse-days-to-claim"] = "10",
            ["b42.server.safehouse-removal-hours"] = "96",
            ["b42.server.faction-enabled"] = "false",
            ["b42.server.faction-days-to-create"] = "5",
            ["b42.server.faction-players-for-tag"] = "3",
            ["b42.server.allow-trade-ui"] = "false",
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
            ["b42.network.steam-vac"] = "false",
            ["b42.network.kick-fast-players"] = "true",
            ["b42.network.deny-login-overloaded"] = "false",
            ["b42.network.player-save-on-damage"] = "false",
            ["b42.network.display-user-name"] = "false",
            ["b42.network.show-first-last-name"] = "true",
            ["b42.network.mouse-over-display-name"] = "true",
            ["b42.network.hide-players-behind-you"] = "true",
            ["b42.network.player-bump-player"] = "false",
            ["b42.network.safety-system"] = "false",
            ["b42.network.show-safety"] = "true",
            ["b42.network.safety-toggle-timer"] = "6",
            ["b42.network.safety-cooldown-timer"] = "30",
            ["b42.network.max-accounts-per-user"] = "3",
            ["b42.network.allow-non-ascii-username"] = "true",
            ["b42.network.voice-enabled"] = "true",
            ["b42.network.voice-3d"] = "false",
            ["b42.network.voice-min-distance"] = "12",
            ["b42.network.voice-max-distance"] = "64",
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
        Assert.Contains("SpawnItems=Base.BaseballBat,Base.WaterBottleFull", iniText);
        Assert.Contains("HoursForLootRespawn=12", iniText);
        Assert.Contains("MaxItemsForLootRespawn=2", iniText);
        Assert.Contains("ConstructionPreventsLootRespawn=false", iniText);
        Assert.Contains("PlayerRespawnWithSelf=true", iniText);
        Assert.Contains("PlayerRespawnWithOther=false", iniText);
        Assert.Contains("HoursForWorldItemRemoval=36.5", iniText);
        Assert.Contains("WorldItemRemovalList=Base.TinCanEmpty,Base.PopBottleEmpty", iniText);
        Assert.Contains("SleepAllowed=true", iniText);
        Assert.Contains("SleepNeeded=true", iniText);
        Assert.Contains("NoFire=true", iniText);
        Assert.Contains("AnnounceDeath=false", iniText);
        Assert.Contains("DropOffWhiteListAfterDeath=true", iniText);
        Assert.Contains("AllowDestructionBySledgehammer=false", iniText);
        Assert.Contains("PlayerSafehouse=true", iniText);
        Assert.Contains("AdminSafehouse=true", iniText);
        Assert.Contains("SafehouseAllowTrepass=false", iniText);
        Assert.Contains("SafehouseAllowFire=false", iniText);
        Assert.Contains("SafehouseAllowLoot=false", iniText);
        Assert.Contains("SafehouseAllowRespawn=true", iniText);
        Assert.Contains("SafehouseDaySurvivedToClaim=10", iniText);
        Assert.Contains("SafeHouseRemovalTime=96", iniText);
        Assert.Contains("Faction=false", iniText);
        Assert.Contains("FactionDaySurvivedToCreate=5", iniText);
        Assert.Contains("FactionPlayersRequiredForTag=3", iniText);
        Assert.Contains("AllowTradeUI=false", iniText);
        Assert.Contains("DefaultPort=16270", iniText);
        Assert.Contains("RCONPort=27025", iniText);
        Assert.Contains("BindIP=10.0.0.25", iniText);
        Assert.Contains("Password=new-join-password", iniText);
        Assert.Contains("RCONPassword=new-rcon-password", iniText);
        Assert.Contains("AutoCreateUserInWhiteList=true", iniText);
        Assert.Contains("DoLuaChecksum=false", iniText);
        Assert.Contains("UPnP=false", iniText);
        Assert.Contains("PingLimit=180", iniText);
        Assert.Contains("SteamVAC=false", iniText);
        Assert.Contains("KickFastPlayers=true", iniText);
        Assert.Contains("DenyLoginOnOverloadedServer=false", iniText);
        Assert.Contains("PlayerSaveOnDamage=false", iniText);
        Assert.Contains("DisplayUserName=false", iniText);
        Assert.Contains("ShowFirstAndLastName=true", iniText);
        Assert.Contains("MouseOverToSeeDisplayName=true", iniText);
        Assert.Contains("HidePlayersBehindYou=true", iniText);
        Assert.Contains("PlayerBumpPlayer=false", iniText);
        Assert.Contains("SafetySystem=false", iniText);
        Assert.Contains("ShowSafety=true", iniText);
        Assert.Contains("SafetyToggleTimer=6", iniText);
        Assert.Contains("SafetyCooldownTimer=30", iniText);
        Assert.Contains("MaxAccountsPerUser=3", iniText);
        Assert.Contains("AllowNonAsciiUsername=true", iniText);
        Assert.Contains("VoiceEnable=true", iniText);
        Assert.Contains("Voice3D=false", iniText);
        Assert.Contains("VoiceMinDistance=12", iniText);
        Assert.Contains("VoiceMaxDistance=64", iniText);

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

    [Fact]
    public async Task Validate_NetworkPageRejectsVoiceDistanceRangesThatCollapse()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-network-validate",
            DisplayName = "Profile Network Validate",
            ServerName = "profile-server",
            InstallDirectory = Path.Combine(_tempRoot, "install"),
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, """
            BindIP=
            Password=
            RCONPassword=
            AutoCreateUserInWhiteList=false
            DoLuaChecksum=true
            UPnP=true
            PingLimit=100
            SteamVAC=true
            KickFastPlayers=false
            DenyLoginOnOverloadedServer=true
            PlayerSaveOnDamage=true
            DisplayUserName=true
            ShowFirstAndLastName=false
            SafetySystem=true
            ShowSafety=true
            SafetyToggleTimer=2
            SafetyCooldownTimer=10
            MaxAccountsPerUser=0
            AllowNonAsciiUsername=false
            VoiceEnable=true
            Voice3D=true
            VoiceMinDistance=10
            VoiceMaxDistance=20
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "network-validate.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var validation = service.Validate(profile, ProfileWorkspacePageIds.NetworkAndAdmin, new Dictionary<string, string?>
        {
            ["b42.network.bind-ip"] = string.Empty,
            ["b42.network.server-password"] = string.Empty,
            ["b42.network.rcon-password"] = string.Empty,
            ["b42.network.auto-whitelist"] = "false",
            ["b42.network.do-lua-checksum"] = "true",
            ["b42.network.upnp"] = "true",
            ["b42.network.ping-limit"] = "100",
            ["b42.network.steam-vac"] = "true",
            ["b42.network.kick-fast-players"] = "false",
            ["b42.network.deny-login-overloaded"] = "true",
            ["b42.network.player-save-on-damage"] = "true",
            ["b42.network.display-user-name"] = "true",
            ["b42.network.show-first-last-name"] = "false",
            ["b42.network.safety-system"] = "true",
            ["b42.network.show-safety"] = "true",
            ["b42.network.safety-toggle-timer"] = "2",
            ["b42.network.safety-cooldown-timer"] = "10",
            ["b42.network.max-accounts-per-user"] = "0",
            ["b42.network.allow-non-ascii-username"] = "false",
            ["b42.network.voice-enabled"] = "true",
            ["b42.network.voice-3d"] = "true",
            ["b42.network.voice-min-distance"] = "30",
            ["b42.network.voice-max-distance"] = "12",
            ["b42.network.admin-user"] = "admin",
            ["b42.network.admin-password"] = string.Empty,
        });

        Assert.False(validation.IsValid);
        Assert.Contains("Voice maximum distance must be greater than or equal to the minimum distance.", validation.FieldErrors["b42.network.voice-max-distance"]);
    }

    [Fact]
    public async Task Sandbox_PageReadsAndWritesExpandedTopLevelFields()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-d",
            DisplayName = "Profile D",
            ServerName = "profile-server",
            InstallDirectory = Path.Combine(_tempRoot, "install"),
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SandboxVarsFilePath)!);
        File.WriteAllText(paths.SandboxVarsFilePath, """
            SandboxVars = {
                VERSION = 4,
                Zombies = 4,
                Distribution = 1,
                DayLength = 3,
                StartYear = 1,
                StartMonth = 4,
                StartDay = 1,
                StartTime = 2,
                WaterShutModifier = 500,
                ElecShutModifier = 480,
                ErosionSpeed = 5,
                LootRespawn = 2,
                FoodLoot = 4,
                WeaponLoot = 2,
                OtherLoot = 3,
                Temperature = 3,
                Rain = 3,
                Alarm = 6,
                LockedHouses = 6,
                Farming = 1,
                StatsDecrease = 4,
                NatureAbundance = 3,
                FoodRotSpeed = 5,
                FridgeFactor = 5,
                PlantResilience = 3,
                PlantAbundance = 3,
                EndRegen = 3,
                Helicopter = 2,
                MetaEvent = 1,
                SleepingEvent = 1,
                GeneratorSpawning = 3,
                CharacterFreePoints = 0,
                ConstructionBonusPoints = 3,
                MultiHit = false,
                AllowExteriorGenerator = false,
                BoneFracture = true,
                AttackBlockMovements = true,
                AllClothesUnlocked = false,
                VehicleEasyUse = false,
                PlayerDamageFromCrash = true,
                FireSpread = true,
                HoursForCorpseRemoval = 216,
                DecayingCorpseHealthImpact = 2,
                BloodLevel = 3,
                ClothingDegradation = 3,
                StarterKit = false,
                Nutrition = false,
                EnableSnowOnGround = true,
                EnableVehicles = true,
            }
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "sandbox-extended.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);

        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.Sandbox);

        Assert.Equal("5", valueSet.Values["b42.sandbox.erosion-speed"]);
        Assert.Equal("2", valueSet.Values["b42.sandbox.loot-respawn"]);
        Assert.Equal("6", valueSet.Values["b42.sandbox.alarm"]);
        Assert.Equal("1", valueSet.Values["b42.sandbox.farming"]);
        Assert.Equal("5", valueSet.Values["b42.sandbox.food-rot-speed"]);
        Assert.Equal("3", valueSet.Values["b42.sandbox.end-regen"]);
        Assert.Equal("2", valueSet.Values["b42.sandbox.helicopter"]);
        Assert.Equal("1", valueSet.Values["b42.sandbox.meta-event"]);
        Assert.Equal("1", valueSet.Values["b42.sandbox.sleeping-event"]);
        Assert.Equal("3", valueSet.Values["b42.sandbox.generator-spawning"]);
        Assert.Equal("0", valueSet.Values["b42.sandbox.character-free-points"]);
        Assert.Equal("3", valueSet.Values["b42.sandbox.construction-bonus-points"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.multi-hit"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.allow-exterior-generator"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.bone-fracture"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.attack-block-movements"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.all-clothes-unlocked"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.vehicle-easy-use"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.player-damage-from-crash"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.fire-spread"]);
        Assert.Equal("216", valueSet.Values["b42.sandbox.hours-for-corpse-removal"]);
        Assert.Equal("2", valueSet.Values["b42.sandbox.decaying-corpse-health-impact"]);
        Assert.Equal("3", valueSet.Values["b42.sandbox.blood-level"]);
        Assert.Equal("3", valueSet.Values["b42.sandbox.clothing-degradation"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.enable-snow-on-ground"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.enable-vehicles"]);

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.Sandbox, new Dictionary<string, string?>(valueSet.Values, StringComparer.Ordinal)
        {
            ["b42.sandbox.erosion-speed"] = "2",
            ["b42.sandbox.loot-respawn"] = "4",
            ["b42.sandbox.alarm"] = "3",
            ["b42.sandbox.locked-houses"] = "4",
            ["b42.sandbox.farming"] = "4",
            ["b42.sandbox.stats-decrease"] = "2",
            ["b42.sandbox.nature-abundance"] = "5",
            ["b42.sandbox.food-rot-speed"] = "2",
            ["b42.sandbox.fridge-factor"] = "4",
            ["b42.sandbox.plant-resilience"] = "4",
            ["b42.sandbox.plant-abundance"] = "5",
            ["b42.sandbox.end-regen"] = "2",
            ["b42.sandbox.helicopter"] = "4",
            ["b42.sandbox.meta-event"] = "2",
            ["b42.sandbox.sleeping-event"] = "3",
            ["b42.sandbox.generator-spawning"] = "5",
            ["b42.sandbox.character-free-points"] = "6",
            ["b42.sandbox.construction-bonus-points"] = "4",
            ["b42.sandbox.multi-hit"] = "true",
            ["b42.sandbox.allow-exterior-generator"] = "true",
            ["b42.sandbox.bone-fracture"] = "false",
            ["b42.sandbox.attack-block-movements"] = "false",
            ["b42.sandbox.all-clothes-unlocked"] = "true",
            ["b42.sandbox.vehicle-easy-use"] = "true",
            ["b42.sandbox.player-damage-from-crash"] = "false",
            ["b42.sandbox.fire-spread"] = "false",
            ["b42.sandbox.hours-for-corpse-removal"] = "120",
            ["b42.sandbox.decaying-corpse-health-impact"] = "4",
            ["b42.sandbox.blood-level"] = "2",
            ["b42.sandbox.clothing-degradation"] = "2",
            ["b42.sandbox.enable-snow-on-ground"] = "false",
            ["b42.sandbox.enable-vehicles"] = "true",
        });

        var sandboxText = File.ReadAllText(paths.SandboxVarsFilePath);

        Assert.True(saveResult.Validation.IsValid);
        Assert.Contains("ErosionSpeed = 2", sandboxText);
        Assert.Contains("LootRespawn = 4", sandboxText);
        Assert.Contains("Alarm = 3", sandboxText);
        Assert.Contains("LockedHouses = 4", sandboxText);
        Assert.Contains("Farming = 4", sandboxText);
        Assert.Contains("StatsDecrease = 2", sandboxText);
        Assert.Contains("NatureAbundance = 5", sandboxText);
        Assert.Contains("FoodRotSpeed = 2", sandboxText);
        Assert.Contains("FridgeFactor = 4", sandboxText);
        Assert.Contains("PlantResilience = 4", sandboxText);
        Assert.Contains("PlantAbundance = 5", sandboxText);
        Assert.Contains("EndRegen = 2", sandboxText);
        Assert.Contains("Helicopter = 4", sandboxText);
        Assert.Contains("MetaEvent = 2", sandboxText);
        Assert.Contains("SleepingEvent = 3", sandboxText);
        Assert.Contains("GeneratorSpawning = 5", sandboxText);
        Assert.Contains("CharacterFreePoints = 6", sandboxText);
        Assert.Contains("ConstructionBonusPoints = 4", sandboxText);
        Assert.Contains("MultiHit = true", sandboxText);
        Assert.Contains("AllowExteriorGenerator = true", sandboxText);
        Assert.Contains("BoneFracture = false", sandboxText);
        Assert.Contains("AttackBlockMovements = false", sandboxText);
        Assert.Contains("AllClothesUnlocked = true", sandboxText);
        Assert.Contains("VehicleEasyUse = true", sandboxText);
        Assert.Contains("PlayerDamageFromCrash = false", sandboxText);
        Assert.Contains("FireSpread = false", sandboxText);
        Assert.Contains("HoursForCorpseRemoval = 120", sandboxText);
        Assert.Contains("DecayingCorpseHealthImpact = 4", sandboxText);
        Assert.Contains("BloodLevel = 2", sandboxText);
        Assert.Contains("ClothingDegradation = 2", sandboxText);
        Assert.Contains("EnableSnowOnGround = false", sandboxText);
        Assert.Contains("EnableVehicles = true", sandboxText);
    }

    [Fact]
    public async Task Sandbox_PageWritesZombieLoreFieldsIntoNestedTable()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-zombie-lore",
            DisplayName = "Profile Zombie Lore",
            ServerName = "profile-zombie-lore",
            InstallDirectory = Path.Combine(_tempRoot, "install-zombie-lore"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-zombie-lore"),
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SandboxVarsFilePath)!);
        File.WriteAllText(paths.SandboxVarsFilePath, """
            SandboxVars = {
                VERSION = 4,
                Zombies = 4,
                ZombieLore = {
                    Speed = 3,
                    Decomp = 1,
                    Smell = 2,
                    TriggerHouseAlarm = false,
                    ThumpNoChasing = false,
                    ThumpOnConstruction = true,
                    ZombiesDragDown = true,
                    ZombiesFenceLunge = true,
                }
            }
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "sandbox-zombie-lore.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);

        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.Sandbox);

        Assert.Equal("3", valueSet.Values["b42.sandbox.zombie-lore-speed"]);
        Assert.Equal("2", valueSet.Values["b42.sandbox.zombie-lore-strength"]);
        Assert.Equal("3", valueSet.Values["b42.sandbox.zombie-lore-cognition"]);
        Assert.Equal("1", valueSet.Values["b42.sandbox.zombie-lore-decomp"]);
        Assert.Equal("2", valueSet.Values["b42.sandbox.zombie-lore-smell"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.zombie-lore-trigger-house-alarm"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.zombie-lore-thump-no-chasing"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.zombie-lore-thump-on-construction"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.zombie-lore-drag-down"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.zombie-lore-fence-lunge"]);

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.Sandbox, new Dictionary<string, string?>(valueSet.Values, StringComparer.Ordinal)
        {
            ["b42.sandbox.zombie-lore-speed"] = "2",
            ["b42.sandbox.zombie-lore-strength"] = "4",
            ["b42.sandbox.zombie-lore-toughness"] = "3",
            ["b42.sandbox.zombie-lore-transmission"] = "2",
            ["b42.sandbox.zombie-lore-mortality"] = "6",
            ["b42.sandbox.zombie-lore-reanimate"] = "1",
            ["b42.sandbox.zombie-lore-cognition"] = "2",
            ["b42.sandbox.zombie-lore-memory"] = "3",
            ["b42.sandbox.zombie-lore-decomp"] = "4",
            ["b42.sandbox.zombie-lore-sight"] = "4",
            ["b42.sandbox.zombie-lore-hearing"] = "2",
            ["b42.sandbox.zombie-lore-smell"] = "3",
            ["b42.sandbox.zombie-lore-trigger-house-alarm"] = "true",
            ["b42.sandbox.zombie-lore-thump-no-chasing"] = "true",
            ["b42.sandbox.zombie-lore-thump-on-construction"] = "false",
            ["b42.sandbox.zombie-lore-drag-down"] = "false",
            ["b42.sandbox.zombie-lore-fence-lunge"] = "false",
        });

        var sandboxText = File.ReadAllText(paths.SandboxVarsFilePath);

        Assert.True(saveResult.Validation.IsValid);
        Assert.Contains("ZombieLore = {", sandboxText);
        Assert.Contains("Speed = 2", sandboxText);
        Assert.Contains("Strength = 4", sandboxText);
        Assert.Contains("Toughness = 3", sandboxText);
        Assert.Contains("Transmission = 2", sandboxText);
        Assert.Contains("Mortality = 6", sandboxText);
        Assert.Contains("Reanimate = 1", sandboxText);
        Assert.Contains("Cognition = 2", sandboxText);
        Assert.Contains("Memory = 3", sandboxText);
        Assert.Contains("Decomp = 4", sandboxText);
        Assert.Contains("Sight = 4", sandboxText);
        Assert.Contains("Hearing = 2", sandboxText);
        Assert.Contains("Smell = 3", sandboxText);
        Assert.Contains("TriggerHouseAlarm = true", sandboxText);
        Assert.Contains("ThumpNoChasing = true", sandboxText);
        Assert.Contains("ThumpOnConstruction = false", sandboxText);
        Assert.Contains("ZombiesDragDown = false", sandboxText);
        Assert.Contains("ZombiesFenceLunge = false", sandboxText);
    }

    [Fact]
    public async Task Sandbox_PageWritesZombiePopulationFieldsIntoNestedTable()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-zombie-population",
            DisplayName = "Profile Zombie Population",
            ServerName = "profile-zombie-population",
            InstallDirectory = Path.Combine(_tempRoot, "install-zombie-population"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-zombie-population"),
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SandboxVarsFilePath)!);
        File.WriteAllText(paths.SandboxVarsFilePath, """
            SandboxVars = {
                VERSION = 4,
                Zombies = 4,
                ZombieConfig = {
                    PopulationMultiplier = 1.0,
                }
            }
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "sandbox-zombie-population.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.Sandbox);

        Assert.Equal("1.0", valueSet.Values["b42.sandbox.population-multiplier"]);
        Assert.Equal("1.0", valueSet.Values["b42.sandbox.population-start-multiplier"]);
        Assert.Equal("28", valueSet.Values["b42.sandbox.population-peak-day"]);

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.Sandbox, new Dictionary<string, string?>(valueSet.Values, StringComparer.Ordinal)
        {
            ["b42.sandbox.population-multiplier"] = "1.8",
            ["b42.sandbox.population-start-multiplier"] = "0.6",
            ["b42.sandbox.population-peak-multiplier"] = "2.5",
            ["b42.sandbox.population-peak-day"] = "45",
            ["b42.sandbox.respawn-hours"] = "96.0",
            ["b42.sandbox.respawn-unseen-hours"] = "18.0",
            ["b42.sandbox.respawn-multiplier"] = "0.15",
            ["b42.sandbox.redistribute-hours"] = "8.0",
            ["b42.sandbox.follow-sound-distance"] = "120",
            ["b42.sandbox.rally-group-size"] = "12",
            ["b42.sandbox.rally-travel-distance"] = "18",
            ["b42.sandbox.rally-group-separation"] = "20",
            ["b42.sandbox.rally-group-radius"] = "5",
        });

        var sandboxText = File.ReadAllText(paths.SandboxVarsFilePath);

        Assert.True(saveResult.Validation.IsValid);
        Assert.Contains("ZombieConfig = {", sandboxText);
        Assert.Contains("PopulationMultiplier = 1.8", sandboxText);
        Assert.Contains("PopulationStartMultiplier = 0.6", sandboxText);
        Assert.Contains("PopulationPeakMultiplier = 2.5", sandboxText);
        Assert.Contains("PopulationPeakDay = 45", sandboxText);
        Assert.Contains("RespawnHours = 96.0", sandboxText);
        Assert.Contains("RespawnUnseenHours = 18.0", sandboxText);
        Assert.Contains("RespawnMultiplier = 0.15", sandboxText);
        Assert.Contains("RedistributeHours = 8.0", sandboxText);
        Assert.Contains("FollowSoundDistance = 120", sandboxText);
        Assert.Contains("RallyGroupSize = 12", sandboxText);
        Assert.Contains("RallyTravelDistance = 18", sandboxText);
        Assert.Contains("RallyGroupSeparation = 20", sandboxText);
        Assert.Contains("RallyGroupRadius = 5", sandboxText);
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
