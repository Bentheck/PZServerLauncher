using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Planning;
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
    public async Task GetCatalog_B42SandboxIncludesCategoriesOptionsAndDefaults()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-b42-catalog",
            DisplayName = "Profile B42",
            ServerName = "profile-b42",
            InstallDirectory = Path.Combine(_tempRoot, "install-b42"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-b42"),
            Branch = ProjectZomboidBranch.Unstable42,
        };

        var planner = new ProjectZomboidServerPlanner();
        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "catalog-b42.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var catalog = service.GetCatalog(profile);
        var sandboxPage = catalog.Pages.Single(page => page.PageId == ProfileWorkspacePageIds.Sandbox);

        Assert.Equal("pz.settings.b42", catalog.CatalogId);
        Assert.Equal(4, catalog.CatalogVersion);

        var dayLength = sandboxPage.Sections.SelectMany(section => section.Fields).Single(field => field.FieldId == "b42.sandbox.day-length");
        Assert.Equal(SettingsFieldControlKind.Select, dayLength.Control);
        Assert.Contains(dayLength.Options, option => option.Value == "4" && option.Label == "1 Hour, 30 Minutes");
        Assert.Equal("1 Hour, 30 Minutes", dayLength.DefaultValue);

        var zombieSection = sandboxPage.Sections.First(section => section.SectionId == "b42.sandbox.zombie.basics");
        Assert.Equal("Zombie", zombieSection.CategoryTitle);
        Assert.Equal(2, zombieSection.CategoryOrder);

    }

    [Fact]
    public async Task Validate_B42Sandbox_UsesCatalogDrivenChoiceBooleanAndIntegerRules()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-b42-validate",
            DisplayName = "Profile B42 Validate",
            ServerName = "profile-b42-validate",
            InstallDirectory = Path.Combine(_tempRoot, "install-b42-validate"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-b42-validate"),
            Branch = ProjectZomboidBranch.Unstable42,
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SandboxVarsFilePath)!);
        File.WriteAllText(paths.SandboxVarsFilePath, """
            SandboxVars = {
                VERSION = 4,
                DayLength = "1 Hour, 30 Minutes",
                StartDay = 9,
                AllowWorldMap = true,
            }
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "validate-b42.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var values = new Dictionary<string, string?>(
            service.GetPage(profile, ProfileWorkspacePageIds.Sandbox).Values,
            StringComparer.Ordinal)
        {
            ["b42.sandbox.day-length"] = "Invalid",
            ["b42.sandbox.start-day"] = "nine",
            ["b42.sandbox.allow-world-map"] = "yes",
        };

        var validation = service.Validate(profile, ProfileWorkspacePageIds.Sandbox, values);

        Assert.False(validation.IsValid);
        Assert.Contains("Day Length (in real time) must use one of the supported options.", validation.FieldErrors["b42.sandbox.day-length"]);
        Assert.Contains("Start Day must be a whole number.", validation.FieldErrors["b42.sandbox.start-day"]);
        Assert.Contains("Allow World Map must be true or false.", validation.FieldErrors["b42.sandbox.allow-world-map"]);
    }

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
            SafehouseAllowNonResidential=true
            DisableSafehouseWhenPlayerConnected=false
            DisableSafehouseWhenPlayerDisconnected=true
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
            ClientCommandFilter=SafehouseOnly
            SaveWorldEveryMinutes=15
            PlayerSaveOnDamage=false
            DisplayUserName=true
            ShowFirstAndLastName=false
            MouseOverToSeeDisplayName=true
            HidePlayersBehindYou=true
            PlayerBumpPlayer=false
            MapRemotePlayerVisibility=2
            UseTCPForMapTraffic=true
            SafetySystem=true
            ShowSafety=false
            SafetyToggleTimer=3
            SafetyCooldownTimer=15
            MaxAccountsPerUser=2
            AllowNonAsciiUsername=true
            Tag=ROLEPLAY
            ResetID=4
            VoiceEnable=true
            Voice3D=true
            VoiceMinDistance=10
            VoiceMaxDistance=55
            MinutesPerPage=3
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
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.safehouse-allow-non-residential"]);
        Assert.Equal("false", generalValues.Values[$"{branchPrefix}.server.disable-safehouse-when-player-connected"]);
        Assert.Equal("true", generalValues.Values[$"{branchPrefix}.server.disable-safehouse-when-player-disconnected"]);
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
        Assert.Equal("SafehouseOnly", networkValues.Values[$"{branchPrefix}.network.client-command-filter"]);
        Assert.Equal("15", networkValues.Values[$"{branchPrefix}.network.save-world-every-minutes"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.player-save-on-damage"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.display-user-name"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.show-first-last-name"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.mouse-over-display-name"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.hide-players-behind-you"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.player-bump-player"]);
        Assert.Equal("2", networkValues.Values[$"{branchPrefix}.network.map-remote-player-visibility"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.use-tcp-for-map-traffic"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.safety-system"]);
        Assert.Equal("false", networkValues.Values[$"{branchPrefix}.network.show-safety"]);
        Assert.Equal("3", networkValues.Values[$"{branchPrefix}.network.safety-toggle-timer"]);
        Assert.Equal("15", networkValues.Values[$"{branchPrefix}.network.safety-cooldown-timer"]);
        Assert.Equal("2", networkValues.Values[$"{branchPrefix}.network.max-accounts-per-user"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.allow-non-ascii-username"]);
        Assert.Equal("ROLEPLAY", networkValues.Values[$"{branchPrefix}.network.server-tag"]);
        Assert.Equal("4", networkValues.Values[$"{branchPrefix}.network.reset-id"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.voice-enabled"]);
        Assert.Equal("true", networkValues.Values[$"{branchPrefix}.network.voice-3d"]);
        Assert.Equal("10", networkValues.Values[$"{branchPrefix}.network.voice-min-distance"]);
        Assert.Equal("55", networkValues.Values[$"{branchPrefix}.network.voice-max-distance"]);
        Assert.Equal("3", networkValues.Values[$"{branchPrefix}.network.minutes-per-page"]);
        Assert.Equal(profile.AdminUsername, networkValues.Values[$"{branchPrefix}.network.admin-user"]);
        Assert.Equal(string.Empty, networkValues.Values[$"{branchPrefix}.network.admin-password"]);
        Assert.False(generalValues.RequiresAdvancedFilesFallback);
        Assert.False(networkValues.RequiresAdvancedFilesFallback);
    }

    [Fact]
    public async Task SaveAsync_GeneralWithoutExistingIni_CreatesInitialIniFromDefaultsAndOverrides()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-general-bootstrap",
            DisplayName = "Profile General Bootstrap",
            ServerName = "profile-general-bootstrap",
            InstallDirectory = Path.Combine(_tempRoot, "install-general-bootstrap"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-general-bootstrap"),
            DefaultPort = 17261,
            UdpPort = 17262,
            RconPort = 28015,
            PreferredMemoryInGigabytes = 10,
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "general-bootstrap.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var generalValues = new Dictionary<string, string?>(
            service.GetPage(profile, ProfileWorkspacePageIds.General).Values,
            StringComparer.Ordinal)
        {
            ["b42.server.public-name"] = "Bootstrap Nights",
            ["b42.server.max-players"] = "24",
            ["b42.server.port"] = "17261",
            ["b42.server.udp-port"] = "17262",
            ["b42.server.rcon-port"] = "28015",
            ["b42.runtime.memory"] = "10",
        };

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.General, generalValues);
        var updatedProfile = await profileStore.GetAsync(profile.ProfileId);

        Assert.True(saveResult.Validation.IsValid);
        Assert.True(File.Exists(paths.IniFilePath));

        var iniText = File.ReadAllText(paths.IniFilePath);
        Assert.Contains("PublicName=Bootstrap Nights", iniText);
        Assert.Contains("MaxPlayers=24", iniText);
        Assert.Contains("DefaultPort=17261", iniText);
        Assert.Contains("RCONPort=28015", iniText);
        Assert.NotNull(updatedProfile);
        Assert.Equal(17261, updatedProfile!.DefaultPort);
        Assert.Equal(17262, updatedProfile.UdpPort);
        Assert.Equal(28015, updatedProfile.RconPort);
        Assert.Equal(10, updatedProfile.PreferredMemoryInGigabytes);
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
            SafehouseAllowNonResidential=false
            DisableSafehouseWhenPlayerConnected=false
            DisableSafehouseWhenPlayerDisconnected=false
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
            ClientCommandFilter=
            SaveWorldEveryMinutes=0
            PlayerSaveOnDamage=true
            DisplayUserName=true
            ShowFirstAndLastName=false
            MouseOverToSeeDisplayName=false
            HidePlayersBehindYou=false
            PlayerBumpPlayer=true
            MapRemotePlayerVisibility=1
            UseTCPForMapTraffic=false
            SafetySystem=true
            ShowSafety=true
            SafetyToggleTimer=2
            SafetyCooldownTimer=10
            MaxAccountsPerUser=0
            AllowNonAsciiUsername=false
            Tag=
            ResetID=0
            VoiceEnable=true
            Voice3D=true
            VoiceMinDistance=8
            VoiceMaxDistance=45
            MinutesPerPage=1
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
            ["b42.server.safehouse-allow-non-residential"] = "true",
            ["b42.server.disable-safehouse-when-player-connected"] = "true",
            ["b42.server.disable-safehouse-when-player-disconnected"] = "false",
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
            ["b42.network.client-command-filter"] = "SafehouseOnly",
            ["b42.network.save-world-every-minutes"] = "20",
            ["b42.network.player-save-on-damage"] = "false",
            ["b42.network.display-user-name"] = "false",
            ["b42.network.show-first-last-name"] = "true",
            ["b42.network.mouse-over-display-name"] = "true",
            ["b42.network.hide-players-behind-you"] = "true",
            ["b42.network.player-bump-player"] = "false",
            ["b42.network.map-remote-player-visibility"] = "3",
            ["b42.network.use-tcp-for-map-traffic"] = "true",
            ["b42.network.safety-system"] = "false",
            ["b42.network.show-safety"] = "true",
            ["b42.network.safety-toggle-timer"] = "6",
            ["b42.network.safety-cooldown-timer"] = "30",
            ["b42.network.max-accounts-per-user"] = "3",
            ["b42.network.allow-non-ascii-username"] = "true",
            ["b42.network.server-tag"] = "COOP42",
            ["b42.network.reset-id"] = "7",
            ["b42.network.voice-enabled"] = "true",
            ["b42.network.voice-3d"] = "false",
            ["b42.network.voice-min-distance"] = "12",
            ["b42.network.voice-max-distance"] = "64",
            ["b42.network.minutes-per-page"] = "5",
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
        Assert.Contains("SafehouseAllowNonResidential=true", iniText);
        Assert.Contains("DisableSafehouseWhenPlayerConnected=true", iniText);
        Assert.Contains("DisableSafehouseWhenPlayerDisconnected=false", iniText);
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
        Assert.Contains("ClientCommandFilter=SafehouseOnly", iniText);
        Assert.Contains("SaveWorldEveryMinutes=20", iniText);
        Assert.Contains("PlayerSaveOnDamage=false", iniText);
        Assert.Contains("DisplayUserName=false", iniText);
        Assert.Contains("ShowFirstAndLastName=true", iniText);
        Assert.Contains("MouseOverToSeeDisplayName=true", iniText);
        Assert.Contains("HidePlayersBehindYou=true", iniText);
        Assert.Contains("PlayerBumpPlayer=false", iniText);
        Assert.Contains("MapRemotePlayerVisibility=3", iniText);
        Assert.Contains("UseTCPForMapTraffic=true", iniText);
        Assert.Contains("SafetySystem=false", iniText);
        Assert.Contains("ShowSafety=true", iniText);
        Assert.Contains("SafetyToggleTimer=6", iniText);
        Assert.Contains("SafetyCooldownTimer=30", iniText);
        Assert.Contains("MaxAccountsPerUser=3", iniText);
        Assert.Contains("AllowNonAsciiUsername=true", iniText);
        Assert.Contains("Tag=COOP42", iniText);
        Assert.Contains("ResetID=7", iniText);
        Assert.Contains("VoiceEnable=true", iniText);
        Assert.Contains("Voice3D=false", iniText);
        Assert.Contains("VoiceMinDistance=12", iniText);
        Assert.Contains("VoiceMaxDistance=64", iniText);
        Assert.Contains("MinutesPerPage=5", iniText);

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
    public async Task SaveAsync_NetworkPagePreservesMissingDoLuaChecksumWhenSubmittedValueMatchesInheritedDefault()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-network-preserve-missing-checksum",
            DisplayName = "Profile Network Preserve Missing Checksum",
            ServerName = "profile-network-preserve-missing-checksum",
            InstallDirectory = Path.Combine(_tempRoot, "install-network-preserve-missing-checksum"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-network-preserve-missing-checksum"),
            BindIp = "0.0.0.0",
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, """
            BindIP=0.0.0.0
            PingLimit=250
            SteamVAC=true
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "network-preserve-missing-checksum.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.NetworkAndAdmin);

        var result = await service.SaveAsync(
            profile,
            ProfileWorkspacePageIds.NetworkAndAdmin,
            new Dictionary<string, string?>(valueSet.Values, StringComparer.Ordinal));

        var iniText = File.ReadAllText(paths.IniFilePath);

        Assert.True(result.Validation.IsValid);
        Assert.DoesNotContain("DoLuaChecksum=", iniText);
    }

    [Fact]
    public async Task SaveAsync_NetworkPageWritesExplicitDoLuaChecksumFalseWhenSourceWasMissing()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-network-write-checksum-false",
            DisplayName = "Profile Network Write Checksum False",
            ServerName = "profile-network-write-checksum-false",
            InstallDirectory = Path.Combine(_tempRoot, "install-network-write-checksum-false"),
            CacheDirectory = Path.Combine(_tempRoot, "cache-network-write-checksum-false"),
            BindIp = "0.0.0.0",
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, """
            BindIP=0.0.0.0
            PingLimit=250
            SteamVAC=true
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "network-write-checksum-false.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var values = new Dictionary<string, string?>(
            service.GetPage(profile, ProfileWorkspacePageIds.NetworkAndAdmin).Values,
            StringComparer.Ordinal)
        {
            ["b42.network.do-lua-checksum"] = "false",
        };

        var result = await service.SaveAsync(profile, ProfileWorkspacePageIds.NetworkAndAdmin, values);
        var iniText = File.ReadAllText(paths.IniFilePath);

        Assert.True(result.Validation.IsValid);
        Assert.Contains("DoLuaChecksum=false", iniText);
    }

    [Fact]
    public async Task ModsAndMaps_PageReadsAndWritesIniBackedPresetValues()
    {
        Directory.CreateDirectory(_tempRoot);
        var installDirectory = Path.Combine(_tempRoot, "install");
        var firstItemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "1234567890", "mods", "ExampleMod");
        Directory.CreateDirectory(Path.Combine(firstItemDirectory, "media", "maps", "RavenCreek"));
        File.WriteAllText(Path.Combine(firstItemDirectory, "mod.info"), "id=ExampleMod\nmap=RavenCreek");
        var secondItemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "2345678901", "mods", "AnotherMod");
        Directory.CreateDirectory(Path.Combine(secondItemDirectory, "media", "maps", "Muldraugh, KY"));
        File.WriteAllText(Path.Combine(secondItemDirectory, "mod.info"), "id=AnotherMod\nmap=Muldraugh, KY");

        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-c",
            DisplayName = "Profile C",
            ServerName = "profile-server",
            InstallDirectory = installDirectory,
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
    public async Task Validate_ReturnsFieldErrorsForExpandedNetworkValues()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-network-expanded-validate",
            DisplayName = "Profile Network Expanded Validate",
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
            ClientCommandFilter=
            SaveWorldEveryMinutes=15
            PlayerSaveOnDamage=true
            DisplayUserName=true
            ShowFirstAndLastName=false
            MouseOverToSeeDisplayName=true
            HidePlayersBehindYou=true
            PlayerBumpPlayer=false
            MapRemotePlayerVisibility=1
            UseTCPForMapTraffic=false
            SafetySystem=true
            ShowSafety=true
            SafetyToggleTimer=2
            SafetyCooldownTimer=10
            MaxAccountsPerUser=0
            AllowNonAsciiUsername=false
            Tag=
            ResetID=0
            VoiceEnable=true
            Voice3D=true
            VoiceMinDistance=10
            VoiceMaxDistance=20
            MinutesPerPage=1
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "network-expanded-validate.db"));
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
            ["b42.network.client-command-filter"] = new string('a', 257),
            ["b42.network.save-world-every-minutes"] = "-1",
            ["b42.network.player-save-on-damage"] = "true",
            ["b42.network.display-user-name"] = "true",
            ["b42.network.show-first-last-name"] = "false",
            ["b42.network.mouse-over-display-name"] = "true",
            ["b42.network.hide-players-behind-you"] = "true",
            ["b42.network.player-bump-player"] = "false",
            ["b42.network.map-remote-player-visibility"] = "-1",
            ["b42.network.use-tcp-for-map-traffic"] = "sometimes",
            ["b42.network.safety-system"] = "true",
            ["b42.network.show-safety"] = "true",
            ["b42.network.safety-toggle-timer"] = "2",
            ["b42.network.safety-cooldown-timer"] = "10",
            ["b42.network.max-accounts-per-user"] = "0",
            ["b42.network.allow-non-ascii-username"] = "false",
            ["b42.network.server-tag"] = new string('x', 33),
            ["b42.network.reset-id"] = "-3",
            ["b42.network.voice-enabled"] = "true",
            ["b42.network.voice-3d"] = "true",
            ["b42.network.voice-min-distance"] = "10",
            ["b42.network.voice-max-distance"] = "20",
            ["b42.network.minutes-per-page"] = "-5",
            ["b42.network.admin-user"] = "admin",
            ["b42.network.admin-password"] = string.Empty,
        });

        Assert.False(validation.IsValid);
        Assert.Contains("Client command filter must stay under 256 characters.", validation.FieldErrors["b42.network.client-command-filter"]);
        Assert.Contains("Save world every minutes must be zero or greater.", validation.FieldErrors["b42.network.save-world-every-minutes"]);
        Assert.Contains("Remote map player visibility must be zero or greater.", validation.FieldErrors["b42.network.map-remote-player-visibility"]);
        Assert.Contains("Use TCP for map traffic must be true or false.", validation.FieldErrors["b42.network.use-tcp-for-map-traffic"]);
        Assert.Contains("Server tag must stay under 32 characters.", validation.FieldErrors["b42.network.server-tag"]);
        Assert.Contains("Reset ID must be zero or greater.", validation.FieldErrors["b42.network.reset-id"]);
        Assert.Contains("Minutes per page must be zero or greater.", validation.FieldErrors["b42.network.minutes-per-page"]);
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
                DayLength = 4,
                StartMonth = 7,
                StartDay = 9,
                StartTime = 2,
                WaterShutModifier = 14,
                ElecShutModifier = 14,
                Alarm = 4,
                LockedHouses = 6,
                FireSpread = true,
                AllowExteriorGenerator = true,
                FoodRotSpeed = 3,
                FridgeFactor = 3,
                ErosionSpeed = 4,
                FarmingSpeedNew = 1.0,
                StatsDecrease = 3,
                NatureAbundance = 3,
                PlantResilience = 3,
                FarmingAmountNew = 1.0,
                EndRegen = 3,
                Helicopter = 2,
                MetaEvent = 2,
                SleepingEvent = 1,
                CharacterFreePoints = 0,
                ConstructionBonusPoints = 3,
                MultiHitZombies = false,
                BoneFracture = true,
                AttackBlockMovements = true,
                AllClothesUnlocked = false,
                VehicleEasyUse = false,
                PlayerDamageFromCrash = true,
                HoursForCorpseRemoval = 216.0,
                DecayingCorpseHealthImpact = 3,
                BloodLevel = 3,
                ClothingDegradation = 3,
                EnableSnowOnGround = true,
                EnableVehicles = true,
            }
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "sandbox-extended.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);

        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.Sandbox);

        Assert.Equal("Slow (200 Days)", valueSet.Values["b42.sandbox.erosion-speed"]);
        Assert.Equal("Sometimes", valueSet.Values["b42.sandbox.alarm"]);
        Assert.Equal("Very Often", valueSet.Values["b42.sandbox.locked-houses"]);
        Assert.Equal("1.0", valueSet.Values["b42.sandbox.farming"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.food-spoilage"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.end-regen"]);
        Assert.Equal("Once", valueSet.Values["b42.sandbox.helicopter"]);
        Assert.Equal("Sometimes", valueSet.Values["b42.sandbox.meta-event"]);
        Assert.Equal("Never", valueSet.Values["b42.sandbox.sleeping-event"]);
        Assert.Equal("0", valueSet.Values["b42.sandbox.character-free-points"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.player-built-construction-strength"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.multi-hit"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.allow-exterior-generator"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.bone-fracture"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.attack-block-movements"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.all-clothes-unlocked"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.vehicle-easy-use"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.player-damage-from-crash"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.fire-spread"]);
        Assert.Equal("216.0", valueSet.Values["b42.sandbox.hours-for-corpse-removal"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.decaying-corpse-health-impact"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.blood-level"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.clothing-degradation"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.enable-snow-on-ground"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.enable-vehicles"]);

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.Sandbox, new Dictionary<string, string?>(valueSet.Values, StringComparer.Ordinal)
        {
            ["b42.sandbox.erosion-speed"] = "Normal (100 Days)",
            ["b42.sandbox.alarm"] = "Very Often",
            ["b42.sandbox.locked-houses"] = "Often",
            ["b42.sandbox.farming"] = "2.5",
            ["b42.sandbox.stats-decrease"] = "Slow",
            ["b42.sandbox.nature-abundance"] = "Abundant",
            ["b42.sandbox.food-spoilage"] = "Slow",
            ["b42.sandbox.refrigeration-effectiveness"] = "Very High",
            ["b42.sandbox.plant-resilience"] = "High",
            ["b42.sandbox.plant-abundance"] = "1.5",
            ["b42.sandbox.end-regen"] = "Fast",
            ["b42.sandbox.helicopter"] = "Often",
            ["b42.sandbox.meta-event"] = "Often",
            ["b42.sandbox.sleeping-event"] = "Sometimes",
            ["b42.sandbox.character-free-points"] = "6",
            ["b42.sandbox.player-built-construction-strength"] = "High",
            ["b42.sandbox.multi-hit"] = "true",
            ["b42.sandbox.allow-exterior-generator"] = "false",
            ["b42.sandbox.bone-fracture"] = "false",
            ["b42.sandbox.attack-block-movements"] = "false",
            ["b42.sandbox.all-clothes-unlocked"] = "true",
            ["b42.sandbox.vehicle-easy-use"] = "true",
            ["b42.sandbox.player-damage-from-crash"] = "false",
            ["b42.sandbox.fire-spread"] = "false",
            ["b42.sandbox.hours-for-corpse-removal"] = "120.0",
            ["b42.sandbox.decaying-corpse-health-impact"] = "High",
            ["b42.sandbox.blood-level"] = "Low",
            ["b42.sandbox.clothing-degradation"] = "Fast",
            ["b42.sandbox.enable-snow-on-ground"] = "false",
            ["b42.sandbox.enable-vehicles"] = "true",
        });

        var sandboxText = File.ReadAllText(paths.SandboxVarsFilePath);

        Assert.True(saveResult.Validation.IsValid);
        Assert.Contains("ErosionSpeed = 3", sandboxText);
        Assert.Contains("Alarm = 6", sandboxText);
        Assert.Contains("LockedHouses = 5", sandboxText);
        Assert.Contains("FarmingSpeedNew = 2.5", sandboxText);
        Assert.Contains("StatsDecrease = 4", sandboxText);
        Assert.Contains("NatureAbundance = 4", sandboxText);
        Assert.Contains("FoodRotSpeed = 4", sandboxText);
        Assert.Contains("FridgeFactor = 5", sandboxText);
        Assert.Contains("PlantResilience = 2", sandboxText);
        Assert.Contains("FarmingAmountNew = 1.5", sandboxText);
        Assert.Contains("EndRegen = 2", sandboxText);
        Assert.Contains("Helicopter = 4", sandboxText);
        Assert.Contains("MetaEvent = 3", sandboxText);
        Assert.Contains("SleepingEvent = 2", sandboxText);
        Assert.Contains("CharacterFreePoints = 6", sandboxText);
        Assert.Contains("ConstructionBonusPoints = 4", sandboxText);
        Assert.Contains("MultiHitZombies = true", sandboxText);
        Assert.Contains("AllowExteriorGenerator = false", sandboxText);
        Assert.Contains("BoneFracture = false", sandboxText);
        Assert.Contains("AttackBlockMovements = false", sandboxText);
        Assert.Contains("AllClothesUnlocked = true", sandboxText);
        Assert.Contains("VehicleEasyUse = true", sandboxText);
        Assert.Contains("PlayerDamageFromCrash = false", sandboxText);
        Assert.Contains("FireSpread = false", sandboxText);
        Assert.Contains("HoursForCorpseRemoval = 120.0", sandboxText);
        Assert.Contains("DecayingCorpseHealthImpact = 4", sandboxText);
        Assert.Contains("BloodLevel = 2", sandboxText);
        Assert.Contains("ClothingDegradation = 4", sandboxText);
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
                ZombieLore = {
                    Speed = 4,
                    Strength = 2,
                    Toughness = 4,
                    Transmission = 1,
                    Mortality = 5,
                    Reanimate = 3,
                    Cognition = 3,
                    DoorOpeningPercentage = 0,
                    CrawlUnderVehicle = 5,
                    Memory = 2,
                    Sight = 5,
                    Hearing = 5,
                    SpottedLogic = true,
                    ThumpNoChasing = false,
                    ThumpOnConstruction = true,
                    ActiveOnly = 1,
                    TriggerHouseAlarm = true,
                    ZombiesDragDown = true,
                    ZombiesCrawlersDragDown = false,
                    ZombiesFenceLunge = true,
                    DisableFakeDead = 1,
                }
            }
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "sandbox-zombie-lore.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);

        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.Sandbox);

        Assert.Equal("Random", valueSet.Values["b42.sandbox.zombie-lore-speed"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.zombie-lore-strength"]);
        Assert.Equal("Basic Navigation", valueSet.Values["b42.sandbox.zombie-lore-cognition"]);
        Assert.Equal("0", valueSet.Values["b42.sandbox.random-door-opening-amount"]);
        Assert.Equal("Often", valueSet.Values["b42.sandbox.crawl-under-vehicle"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.zombie-lore-memory"]);
        Assert.Equal("Random between Normal and Poor", valueSet.Values["b42.sandbox.zombie-lore-sight"]);
        Assert.Equal("Random between Normal and Poor", valueSet.Values["b42.sandbox.zombie-lore-hearing"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.new-stealth-system"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.environmental-attacks"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.damage-construction"]);
        Assert.Equal("Both", valueSet.Values["b42.sandbox.day-night-zombie-speed-effect"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.zombie-house-alarm-triggering"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.drag-down"]);
        Assert.Equal("false", valueSet.Values["b42.sandbox.crawlers-drag-down"]);
        Assert.Equal("true", valueSet.Values["b42.sandbox.zombie-lunge"]);
        Assert.Equal("World Zombies", valueSet.Values["b42.sandbox.fake-dead-zombie-reanimation"]);

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.Sandbox, new Dictionary<string, string?>(valueSet.Values, StringComparer.Ordinal)
        {
            ["b42.sandbox.zombie-lore-speed"] = "Sprinters",
            ["b42.sandbox.zombie-lore-strength"] = "Superhuman",
            ["b42.sandbox.zombie-lore-toughness"] = "Fragile",
            ["b42.sandbox.zombie-lore-transmission"] = "None",
            ["b42.sandbox.zombie-lore-mortality"] = "Never",
            ["b42.sandbox.zombie-lore-reanimate"] = "Instant",
            ["b42.sandbox.zombie-lore-cognition"] = "Navigate and Use Doors",
            ["b42.sandbox.random-door-opening-amount"] = "25",
            ["b42.sandbox.crawl-under-vehicle"] = "Extremely Rare",
            ["b42.sandbox.zombie-lore-memory"] = "Long",
            ["b42.sandbox.zombie-lore-sight"] = "Eagle",
            ["b42.sandbox.zombie-lore-hearing"] = "Pinpoint",
            ["b42.sandbox.new-stealth-system"] = "false",
            ["b42.sandbox.environmental-attacks"] = "true",
            ["b42.sandbox.damage-construction"] = "false",
            ["b42.sandbox.day-night-zombie-speed-effect"] = "Night",
            ["b42.sandbox.zombie-house-alarm-triggering"] = "false",
            ["b42.sandbox.drag-down"] = "false",
            ["b42.sandbox.crawlers-drag-down"] = "true",
            ["b42.sandbox.zombie-lunge"] = "false",
            ["b42.sandbox.fake-dead-zombie-reanimation"] = "World and Combat Zombies",
        });

        var sandboxText = File.ReadAllText(paths.SandboxVarsFilePath);

        Assert.True(saveResult.Validation.IsValid);
        Assert.Contains("ZombieLore = {", sandboxText);
        Assert.Contains("Speed = 1", sandboxText);
        Assert.Contains("Strength = 1", sandboxText);
        Assert.Contains("Toughness = 3", sandboxText);
        Assert.Contains("Transmission = 4", sandboxText);
        Assert.Contains("Mortality = 7", sandboxText);
        Assert.Contains("Reanimate = 1", sandboxText);
        Assert.Contains("Cognition = 1", sandboxText);
        Assert.Contains("DoorOpeningPercentage = 25", sandboxText);
        Assert.Contains("CrawlUnderVehicle = 2", sandboxText);
        Assert.Contains("Memory = 1", sandboxText);
        Assert.Contains("Sight = 1", sandboxText);
        Assert.Contains("Hearing = 1", sandboxText);
        Assert.Contains("SpottedLogic = false", sandboxText);
        Assert.Contains("ThumpNoChasing = true", sandboxText);
        Assert.Contains("ThumpOnConstruction = false", sandboxText);
        Assert.Contains("ActiveOnly = 2", sandboxText);
        Assert.Contains("TriggerHouseAlarm = false", sandboxText);
        Assert.Contains("ZombiesDragDown = false", sandboxText);
        Assert.Contains("ZombiesCrawlersDragDown = true", sandboxText);
        Assert.Contains("ZombiesFenceLunge = false", sandboxText);
        Assert.Contains("DisableFakeDead = 2", sandboxText);
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
                ZombieConfig = {
                    PopulationMultiplier = 0.65,
                    PopulationStartMultiplier = 1.0,
                    PopulationPeakMultiplier = 1.5,
                    PopulationPeakDay = 28,
                    RespawnHours = 0.0,
                    RespawnUnseenHours = 0.0,
                    RespawnMultiplier = 0.0,
                    RedistributeHours = 12.0,
                    FollowSoundDistance = 100,
                    RallyGroupSize = 20,
                    RallyTravelDistance = 20,
                    RallyGroupSeparation = 15,
                    RallyGroupRadius = 3,
                }
            }
            """);

        await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "sandbox-zombie-population.db"));
        var profileStore = new ProfileStore(dbContext);
        await profileStore.UpsertAsync(profile);

        var service = CreateService(profileStore, planner);
        var valueSet = service.GetPage(profile, ProfileWorkspacePageIds.Sandbox);

        Assert.Equal("Normal", valueSet.Values["b42.sandbox.population-multiplier"]);
        Assert.Equal("Normal", valueSet.Values["b42.sandbox.population-start-multiplier"]);
        Assert.Equal("28", valueSet.Values["b42.sandbox.population-peak-day"]);

        var saveResult = await service.SaveAsync(profile, ProfileWorkspacePageIds.Sandbox, new Dictionary<string, string?>(valueSet.Values, StringComparer.Ordinal)
        {
            ["b42.sandbox.population-multiplier"] = "High",
            ["b42.sandbox.population-start-multiplier"] = "Low",
            ["b42.sandbox.population-peak-multiplier"] = "Insane",
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
        Assert.Contains("PopulationMultiplier = 1.2", sandboxText);
        Assert.Contains("PopulationStartMultiplier = 0.5", sandboxText);
        Assert.Contains("PopulationPeakMultiplier = 3.0", sandboxText);
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

    [Fact]
    public async Task ImportEditAndLaunchPlanAsync_RoundTripsRealServerWorkflow()
    {
        var cacheRoot = Path.Combine(_tempRoot, "Zomboid");
        var serverDirectory = Path.Combine(cacheRoot, "Server");
        Directory.CreateDirectory(serverDirectory);

        File.WriteAllText(
            Path.Combine(serverDirectory, "servertest.ini"),
            """
            PublicName=Community Nights
            PublicDescription=Vanilla and relaxed
            Public=true
            Open=true
            MaxPlayers=16
            PVP=false
            PauseEmpty=true
            SleepAllowed=true
            SleepNeeded=false
            PlayerSafehouse=true
            SafehouseAllowTrepass=true
            SafehouseAllowFire=false
            SafehouseAllowLoot=false
            SafehouseAllowRespawn=true
            SafehouseAllowNonResidential=false
            DisableSafehouseWhenPlayerConnected=false
            DisableSafehouseWhenPlayerDisconnected=true
            SafehouseDaySurvivedToClaim=7
            SafeHouseRemovalTime=144
            DefaultPort=17261
            UDPPort=17262
            RCONPort=28015
            BindIP=127.0.0.1
            Password=
            RCONPassword=
            AdminUsername=admin
            AutoCreateUserInWhiteList=false
            ClientCommandFilter=
            SaveWorldEveryMinutes=0
            MapRemotePlayerVisibility=1
            UseTCPForMapTraffic=false
            VoiceEnable=true
            Voice3D=true
            VoiceMinDistance=8
            VoiceMaxDistance=45
            WorkshopItems=1234567890
            Mods=ExampleMod
            Map=RavenCreek
            """);

        File.WriteAllText(
            Path.Combine(serverDirectory, "servertest_SandboxVars.lua"),
            """
            SandboxVars = {
                VERSION = 4,
                DayLength = 4,
                StartMonth = 7,
                StartDay = 9,
                StartTime = 2,
                WaterShutModifier = 14,
                ElecShutModifier = 14,
                ErosionSpeed = 4,
                HoursForLootRespawn = 0,
                Helicopter = 2,
                MultiHitZombies = false,
            }
            """);

        var installDirectory = CreateInstallDirectory(
            """
            @echo off
            setlocal
            set "JAVA_HOME=%~dp0jre64"
            "%JAVA_HOME%\bin\java.exe" ^
              -Dzomboid.steam=1 ^
              -Djava.awt.headless=true ^
              -Xms2048m ^
              -Xmx2048m ^
              -cp "%~dp0zombie.jar;%~dp0lib\*" ^
              zombie.network.GameServer ^
              -cachedir "%UserProfile%\Zomboid" ^
              -servername servertest
            """);

        try
        {
            var workshopModDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "1234567890", "mods", "ExampleMod");
            Directory.CreateDirectory(workshopModDirectory);
            File.WriteAllText(Path.Combine(workshopModDirectory, "mod.info"), "id=ExampleMod\nmap=RavenCreek");
            Directory.CreateDirectory(Path.Combine(workshopModDirectory, "media", "maps", "RavenCreek"));

            await using var dbContext = TestDatabaseFactory.Create(Path.Combine(_tempRoot, "workflow.db"));
            var profileStore = new ProfileStore(dbContext);
            var workshopScannerService = new WorkshopPresetScannerService();
            var importer = new LocalServerImportService(
                profileStore,
                workshopScannerService,
                cacheRoot,
                installDirectory,
                ProjectZomboidBranch.Unstable42);
            var planner = new ProjectZomboidServerPlanner();
            var structuredSettings = new StructuredSettingsService(
                profileStore,
                new ConfigFileService(planner),
                new ProjectZomboidSettingsCatalogResolver(),
                new IniDocumentService(),
                new SandboxVarsDocumentService(),
                workshopScannerService);

            var candidate = Assert.Single(await importer.DiscoverAsync());
            Assert.False(candidate.IsAlreadyImported);
            Assert.Equal(ProjectZomboidBranch.Unstable42, candidate.Branch);
            Assert.Equal(["1234567890"], candidate.WorkshopPreset.WorkshopItemIds);
            Assert.Equal(["ExampleMod"], candidate.WorkshopPreset.EnabledModIds);
            Assert.Equal(["RavenCreek"], candidate.WorkshopPreset.MapFolders);

            var importedProfile = await importer.ImportAsync(candidate.CandidateId);

            var generalValues = new Dictionary<string, string?>(
                structuredSettings.GetPage(importedProfile, ProfileWorkspacePageIds.General).Values,
                StringComparer.Ordinal);
            generalValues["b42.server.public-name"] = "Night Watch";
            generalValues["b42.server.public-description"] = "Hard nights only";
            generalValues["b42.server.max-players"] = "24";
            generalValues["b42.server.safehouse-allow-non-residential"] = "true";
            generalValues["b42.server.disable-safehouse-when-player-connected"] = "true";
            generalValues["b42.server.disable-safehouse-when-player-disconnected"] = "false";
            generalValues["b42.server.safehouse-days-to-claim"] = "3";
            generalValues["b42.runtime.memory"] = "10";
            generalValues["b42.runtime.start-with-host"] = "true";

            var generalSave = await structuredSettings.SaveAsync(importedProfile, ProfileWorkspacePageIds.General, generalValues);
            Assert.True(generalSave.Validation.IsValid);

            importedProfile = (await profileStore.GetAsync(importedProfile.ProfileId))!;

            var networkValues = new Dictionary<string, string?>(
                structuredSettings.GetPage(importedProfile, ProfileWorkspacePageIds.NetworkAndAdmin).Values,
                StringComparer.Ordinal);
            networkValues["b42.network.bind-ip"] = "10.10.0.8";
            networkValues["b42.network.server-password"] = "join-pass";
            networkValues["b42.network.rcon-password"] = "rcon-pass";
            networkValues["b42.network.client-command-filter"] = "SafehouseOnly";
            networkValues["b42.network.save-world-every-minutes"] = "20";
            networkValues["b42.network.map-remote-player-visibility"] = "2";
            networkValues["b42.network.use-tcp-for-map-traffic"] = "true";
            networkValues["b42.network.voice-enabled"] = "false";
            networkValues["b42.network.voice-3d"] = "false";
            networkValues["b42.network.voice-min-distance"] = "12";
            networkValues["b42.network.voice-max-distance"] = "30";
            networkValues["b42.network.admin-user"] = "warden";
            networkValues["b42.network.admin-password"] = "fresh-secret";

            var networkSave = await structuredSettings.SaveAsync(importedProfile, ProfileWorkspacePageIds.NetworkAndAdmin, networkValues);
            Assert.True(networkSave.Validation.IsValid);

            importedProfile = (await profileStore.GetAsync(importedProfile.ProfileId))!;

            var sandboxValues = new Dictionary<string, string?>(
                structuredSettings.GetPage(importedProfile, ProfileWorkspacePageIds.Sandbox).Values,
                StringComparer.Ordinal);
            sandboxValues["b42.sandbox.erosion-speed"] = "Normal (100 Days)";
            sandboxValues["b42.sandbox.hours-for-loot-respawn"] = "6";
            sandboxValues["b42.sandbox.helicopter"] = "Often";
            sandboxValues["b42.sandbox.multi-hit"] = "true";

            var sandboxSave = await structuredSettings.SaveAsync(importedProfile, ProfileWorkspacePageIds.Sandbox, sandboxValues);
            Assert.True(sandboxSave.Validation.IsValid);

            importedProfile = (await profileStore.GetAsync(importedProfile.ProfileId))!;

            var paths = planner.ResolvePaths(importedProfile);
            var iniText = File.ReadAllText(paths.IniFilePath);
            var sandboxText = File.ReadAllText(paths.SandboxVarsFilePath);

            Assert.Contains("PublicName=Night Watch", iniText);
            Assert.Contains("PublicDescription=Hard nights only", iniText);
            Assert.Contains("MaxPlayers=24", iniText);
            Assert.Contains("SafehouseAllowNonResidential=true", iniText);
            Assert.Contains("DisableSafehouseWhenPlayerConnected=true", iniText);
            Assert.Contains("DisableSafehouseWhenPlayerDisconnected=false", iniText);
            Assert.Contains("SafehouseDaySurvivedToClaim=3", iniText);
            Assert.Contains("BindIP=10.10.0.8", iniText);
            Assert.Contains("Password=join-pass", iniText);
            Assert.Contains("RCONPassword=rcon-pass", iniText);
            Assert.Contains("ClientCommandFilter=SafehouseOnly", iniText);
            Assert.Contains("SaveWorldEveryMinutes=20", iniText);
            Assert.Contains("MapRemotePlayerVisibility=2", iniText);
            Assert.Contains("UseTCPForMapTraffic=true", iniText);
            Assert.Contains("VoiceEnable=false", iniText);
            Assert.Contains("Voice3D=false", iniText);
            Assert.Contains("VoiceMinDistance=12", iniText);
            Assert.Contains("VoiceMaxDistance=30", iniText);

            Assert.Contains("ErosionSpeed = 3", sandboxText);
            Assert.Contains("HoursForLootRespawn = 6", sandboxText);
            Assert.Contains("Helicopter = 4", sandboxText);
            Assert.Contains("MultiHitZombies = true", sandboxText);

            Assert.Equal("warden", importedProfile.AdminUsername);
            Assert.Equal("fresh-secret", importedProfile.AdminPassword);
            Assert.Equal("10.10.0.8", importedProfile.BindIp);
            Assert.Equal(10, importedProfile.PreferredMemoryInGigabytes);
            Assert.True(importedProfile.StartWithHost);

            var launchPlan = planner.CreateLaunchPlan(importedProfile);

            Assert.Equal(ServerLaunchStrategy.DirectJavaTemplate, launchPlan.Strategy);
            Assert.EndsWith(Path.Combine("jre64", "bin", "java.exe"), launchPlan.LauncherPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("-Xms10g", launchPlan.Arguments);
            Assert.Contains("-Xmx10g", launchPlan.Arguments);
            Assert.Contains(launchPlan.Arguments, argument => string.Equals(argument, $"-cachedir={cacheRoot}", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("-servername", launchPlan.Arguments);
            Assert.Contains("servertest", launchPlan.Arguments);
            Assert.Contains("-adminusername", launchPlan.Arguments);
            Assert.Contains("warden", launchPlan.Arguments);
            Assert.Contains("-adminpassword", launchPlan.Arguments);
            Assert.Contains("fresh-secret", launchPlan.Arguments);
            Assert.Contains("-ip", launchPlan.Arguments);
            Assert.Contains("10.10.0.8", launchPlan.Arguments);
        }
        finally
        {
            if (Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, recursive: true);
            }
        }
    }

    private static StructuredSettingsService CreateService(ProfileStore profileStore, ProjectZomboidServerPlanner planner) =>
        new(
            profileStore,
            new ConfigFileService(planner),
            new ProjectZomboidSettingsCatalogResolver(),
            new IniDocumentService(),
            new SandboxVarsDocumentService(),
            new WorkshopPresetScannerService());

    private static string CreateInstallDirectory(string batchFileContent)
    {
        var installDirectory = Path.Combine(Path.GetTempPath(), $"pz-workflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(Path.Combine(installDirectory, "jre64", "bin"));
        File.WriteAllText(Path.Combine(installDirectory, "jre64", "bin", "java.exe"), string.Empty);
        File.WriteAllText(Path.Combine(installDirectory, "StartServer64.bat"), batchFileContent);
        return installDirectory;
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
