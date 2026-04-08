using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

public sealed class ProjectZomboidSettingsCatalogResolver : ISettingsCatalogResolver
{
    private static readonly StructuredSettingsCatalog Stable41Catalog = BuildCatalog(
        catalogId: "pz.settings.b41",
        version: 1,
        branch: ProjectZomboidBranch.Stable41,
        branchPrefix: "b41");

    private static readonly StructuredSettingsCatalog Unstable42Catalog = BuildCatalog(
        catalogId: "pz.settings.b42",
        version: 1,
        branch: ProjectZomboidBranch.Unstable42,
        branchPrefix: "b42");

    public StructuredSettingsCatalog Resolve(ProjectZomboidBranch branch) =>
        branch switch
        {
            ProjectZomboidBranch.Stable41 => Stable41Catalog,
            ProjectZomboidBranch.Unstable42 => Unstable42Catalog,
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unsupported Project Zomboid branch."),
        };

    private static StructuredSettingsCatalog BuildCatalog(string catalogId, int version, ProjectZomboidBranch branch, string branchPrefix)
    {
        var pages = new[]
        {
            BuildGeneralPage(branchPrefix),
            BuildSandboxPage(branchPrefix),
            BuildModsAndMapsPage(branchPrefix),
            BuildNetworkPage(branchPrefix),
            new StructuredPageDefinition($"{branchPrefix}.advanced-files", "Advanced Files", Array.Empty<StructuredSectionDefinition>()),
        };

        return new StructuredSettingsCatalog(catalogId, version, branch, pages);
    }

    private static StructuredPageDefinition BuildGeneralPage(string branchPrefix)
    {
        return new StructuredPageDefinition(
            $"{branchPrefix}.general",
            "General",
            new[]
            {
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.identity",
                    "Server Browser Identity",
                    new[]
                    {
                        Field($"{branchPrefix}.server.public-name", "Public Server Name", StructuredValueKind.Text, ConfigFileKind.Ini, "PublicName", helpText: "This is the visible server name shown in the browser."),
                        Field($"{branchPrefix}.server.public-description", "Public Description", StructuredValueKind.Text, ConfigFileKind.Ini, "PublicDescription", helpText: "Short browser description for players browsing public servers."),
                        Field($"{branchPrefix}.server.public", "Public Listing Enabled", StructuredValueKind.Boolean, ConfigFileKind.Ini, "Public", defaultValue: "true", helpText: "Show the server on Steam and in the public browser."),
                        Field($"{branchPrefix}.server.open", "Open To New Accounts", StructuredValueKind.Boolean, ConfigFileKind.Ini, "Open", defaultValue: "true", helpText: "Allow players to create accounts without a manual whitelist step."),
                        Field($"{branchPrefix}.server.max-players", "Max Players", StructuredValueKind.Integer, ConfigFileKind.Ini, "MaxPlayers", defaultValue: "16", helpText: "Maximum number of survivors allowed to connect."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.world-access",
                    "World Access",
                    new[]
                    {
                        Field($"{branchPrefix}.server.pvp", "PvP Enabled", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PVP", defaultValue: "true", helpText: "Allow player versus player combat on the server."),
                        Field($"{branchPrefix}.server.pause-empty", "Pause When Empty", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PauseEmpty", defaultValue: "true", helpText: "Pause world simulation when nobody is online."),
                        Field($"{branchPrefix}.server.global-chat", "Global Chat", StructuredValueKind.Boolean, ConfigFileKind.Ini, "GlobalChat", defaultValue: "true", helpText: "Allow server-wide chat streams."),
                        Field($"{branchPrefix}.server.welcome-message", "Welcome Message", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "ServerWelcomeMessage", helpText: "Use line breaks here; they will be written back as <LINE> markers for Project Zomboid."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.survival-rules",
                    "Survival Rules",
                    new[]
                    {
                        Field($"{branchPrefix}.server.sleep-allowed", "Sleep Allowed", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SleepAllowed", defaultValue: "false", helpText: "Allow sleeping on the server."),
                        Field($"{branchPrefix}.server.sleep-needed", "Sleep Needed", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SleepNeeded", defaultValue: "false", helpText: "Require survivors to sleep when exhausted."),
                        Field($"{branchPrefix}.server.no-fire", "Disable Fire", StructuredValueKind.Boolean, ConfigFileKind.Ini, "NoFire", defaultValue: "false", helpText: "Disable most fire spread and fire damage on the server."),
                        Field($"{branchPrefix}.server.announce-death", "Announce Death", StructuredValueKind.Boolean, ConfigFileKind.Ini, "AnnounceDeath", defaultValue: "true", helpText: "Broadcast character deaths to the whole server."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.safehouses",
                    "Safehouses",
                    new[]
                    {
                        Field($"{branchPrefix}.server.player-safehouse", "Player Safehouses", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PlayerSafehouse", defaultValue: "true", helpText: "Allow players to claim and use safehouses."),
                        Field($"{branchPrefix}.server.admin-safehouse", "Admin Safehouses", StructuredValueKind.Boolean, ConfigFileKind.Ini, "AdminSafehouse", defaultValue: "false", helpText: "Allow admins to claim safehouses."),
                        Field($"{branchPrefix}.server.safehouse-allow-trespass", "Allow Trespass", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SafehouseAllowTrepass", defaultValue: "true", helpText: "Allow non-members to enter safehouses."),
                        Field($"{branchPrefix}.server.safehouse-allow-fire", "Allow Fire Damage", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SafehouseAllowFire", defaultValue: "true", helpText: "Allow fire to damage safehouses."),
                        Field($"{branchPrefix}.server.safehouse-allow-loot", "Allow Looting", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SafehouseAllowLoot", defaultValue: "true", helpText: "Allow non-members to loot safehouses."),
                        Field($"{branchPrefix}.server.safehouse-allow-respawn", "Allow Respawn", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SafehouseAllowRespawn", defaultValue: "false", helpText: "Allow members to respawn in safehouses."),
                        Field($"{branchPrefix}.server.safehouse-days-to-claim", "Days To Claim", StructuredValueKind.Integer, ConfigFileKind.Ini, "SafehouseDaySurvivedToClaim", defaultValue: "0", helpText: "Days a player must survive before claiming a safehouse."),
                        Field($"{branchPrefix}.server.safehouse-removal-hours", "Removal Time (Hours)", StructuredValueKind.Integer, ConfigFileKind.Ini, "SafeHouseRemovalTime", defaultValue: "144", helpText: "Hours before inactive members are removed from a safehouse."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.factions-and-trade",
                    "Factions & Trade",
                    new[]
                    {
                        Field($"{branchPrefix}.server.faction-enabled", "Factions Enabled", StructuredValueKind.Boolean, ConfigFileKind.Ini, "Faction", defaultValue: "true", helpText: "Allow players to create and join factions."),
                        Field($"{branchPrefix}.server.faction-days-to-create", "Days To Create Faction", StructuredValueKind.Integer, ConfigFileKind.Ini, "FactionDaySurvivedToCreate", defaultValue: "0", helpText: "Days a player must survive before creating a faction."),
                        Field($"{branchPrefix}.server.faction-players-for-tag", "Players Required For Tag", StructuredValueKind.Integer, ConfigFileKind.Ini, "FactionPlayersRequiredForTag", defaultValue: "1", helpText: "Members needed before a faction gets its tag."),
                        Field($"{branchPrefix}.server.allow-trade-ui", "Allow Trade UI", StructuredValueKind.Boolean, ConfigFileKind.Ini, "AllowTradeUI", defaultValue: "true", helpText: "Allow players to use the direct trade UI."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.ports",
                    "Ports & Runtime",
                    new[]
                    {
                        Field($"{branchPrefix}.server.port", "Default Port", StructuredValueKind.Integer, ConfigFileKind.Ini, "DefaultPort", helpText: "Primary server port exposed to clients."),
                        Field($"{branchPrefix}.server.udp-port", "UDP Port Override", StructuredValueKind.Integer, ConfigFileKind.Ini, "UDPPort", helpText: "Launcher-managed UDP override used when starting the server."),
                        Field($"{branchPrefix}.server.rcon-port", "RCON Port", StructuredValueKind.Integer, ConfigFileKind.Ini, "RCONPort", helpText: "Remote console port for administration tools."),
                        Field($"{branchPrefix}.runtime.memory", "Preferred Memory (GB)", StructuredValueKind.Integer, ConfigFileKind.Ini, "PreferredMemoryInGigabytes", restartRequired: true),
                        Field($"{branchPrefix}.runtime.start-with-host", "Start With Host", StructuredValueKind.Boolean, ConfigFileKind.Ini, "StartWithHost"),
                        Field($"{branchPrefix}.runtime.auto-restart", "Auto Restart On Crash", StructuredValueKind.Boolean, ConfigFileKind.Ini, "AutoRestartOnCrash"),
                    }),
            });
    }

    private static StructuredPageDefinition BuildSandboxPage(string branchPrefix)
    {
        return new StructuredPageDefinition(
            $"{branchPrefix}.sandbox",
            "Sandbox",
            new[]
            {
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.world-setup",
                    "World Setup",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.zombies", "Zombie Spawn Rate", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "Zombies", defaultValue: "4", helpText: "1 is most, 5 is none."),
                        Field($"{branchPrefix}.sandbox.distribution", "Zombie Distribution", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "Distribution", defaultValue: "1", helpText: "1 is urban focused, 2 is uniform."),
                        Field($"{branchPrefix}.sandbox.day-length", "Day Length", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "DayLength", defaultValue: "3", helpText: "1 is 15 minutes, 3 is 1 hour, 9 is real-time."),
                        Field($"{branchPrefix}.sandbox.start-year", "Start Year", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "StartYear", defaultValue: "1", helpText: "1 is the first post-apocalypse year."),
                        Field($"{branchPrefix}.sandbox.start-month", "Start Month", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "StartMonth", defaultValue: "4", helpText: "1 is January, 12 is December."),
                        Field($"{branchPrefix}.sandbox.start-day", "Start Day", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "StartDay", defaultValue: "1", helpText: "1 is the first day of the month."),
                        Field($"{branchPrefix}.sandbox.start-time", "Start Time", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "StartTime", defaultValue: "2", helpText: "1 is 7AM, 5 is 5PM, 9 is 5AM."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.utilities",
                    "Utilities",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.water-shut-modifier", "Water Shutoff Day", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "WaterShutModifier", defaultValue: "500", helpText: "-1 is instant. Otherwise use the number of days before water shuts off."),
                        Field($"{branchPrefix}.sandbox.electricity-shut-modifier", "Electricity Shutoff Day", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ElecShutModifier", defaultValue: "480", helpText: "-1 is instant. Otherwise use the number of days before electricity shuts off."),
                        Field($"{branchPrefix}.sandbox.erosion-speed", "Erosion Speed", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ErosionSpeed", defaultValue: "5", helpText: "1 is overgrown quickly, 5 is very slow."),
                        Field($"{branchPrefix}.sandbox.loot-respawn", "Loot Respawn", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "LootRespawn", defaultValue: "2", helpText: "1 is none, 2 is every day, 5 is every two months."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.loot-and-climate",
                    "Loot & Climate",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.food-loot", "Food Loot", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "FoodLoot", defaultValue: "4", helpText: "1 is extremely rare, 5 is abundant."),
                        Field($"{branchPrefix}.sandbox.weapon-loot", "Weapon Loot", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "WeaponLoot", defaultValue: "2", helpText: "1 is extremely rare, 5 is abundant."),
                        Field($"{branchPrefix}.sandbox.other-loot", "Other Loot", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "OtherLoot", defaultValue: "3", helpText: "1 is extremely rare, 5 is abundant."),
                        Field($"{branchPrefix}.sandbox.temperature", "Temperature", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "Temperature", defaultValue: "3", helpText: "1 is very cold, 5 is very hot."),
                        Field($"{branchPrefix}.sandbox.rain", "Rain", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "Rain", defaultValue: "3", helpText: "1 is very dry, 5 is very rainy."),
                        Field($"{branchPrefix}.sandbox.alarm", "House Alarm Frequency", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "Alarm", defaultValue: "6", helpText: "1 is never, 6 is very often."),
                        Field($"{branchPrefix}.sandbox.locked-houses", "Locked Houses", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "LockedHouses", defaultValue: "6", helpText: "1 is never, 6 is very often."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.survival-systems",
                    "Survival Systems",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.farming", "Farming Speed", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "Farming", defaultValue: "1", helpText: "1 is very fast, 5 is very slow."),
                        Field($"{branchPrefix}.sandbox.stats-decrease", "Stats Decrease", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "StatsDecrease", defaultValue: "4", helpText: "1 is very fast, 5 is very slow."),
                        Field($"{branchPrefix}.sandbox.nature-abundance", "Nature Abundance", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "NatureAbundance", defaultValue: "3", helpText: "1 is very poor, 5 is very abundant."),
                        Field($"{branchPrefix}.sandbox.food-rot-speed", "Food Rot Speed", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "FoodRotSpeed", defaultValue: "5", helpText: "1 is very fast, 5 is very slow."),
                        Field($"{branchPrefix}.sandbox.fridge-factor", "Fridge Factor", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "FridgeFactor", defaultValue: "5", helpText: "1 is very low, 5 is very high."),
                        Field($"{branchPrefix}.sandbox.plant-resilience", "Plant Resilience", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "PlantResilience", defaultValue: "3", helpText: "1 is very low, 5 is very high."),
                        Field($"{branchPrefix}.sandbox.plant-abundance", "Plant Abundance", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "PlantAbundance", defaultValue: "3", helpText: "1 is very poor, 5 is very abundant."),
                        Field($"{branchPrefix}.sandbox.end-regen", "Endurance Regen", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "EndRegen", defaultValue: "3", helpText: "1 is very fast, 5 is very slow."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.player-experience",
                    "Player Experience",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.starter-kit", "Starter Kit", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "StarterKit", defaultValue: "false", helpText: "Enable the beginner starter kit."),
                        Field($"{branchPrefix}.sandbox.nutrition", "Nutrition", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "Nutrition", defaultValue: "false", helpText: "Track calories, weight, and nutrition in multiplayer."),
                    }),
            });
    }

    private static StructuredPageDefinition BuildModsAndMapsPage(string branchPrefix)
    {
        return new StructuredPageDefinition(
            $"{branchPrefix}.mods-and-maps",
            "Mods & Maps",
            new[]
            {
                new StructuredSectionDefinition(
                    $"{branchPrefix}.mods-and-maps.collection",
                    "Collection",
                    new[]
                    {
                        Field($"{branchPrefix}.mods.workshop-items", "Workshop Item IDs", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "WorkshopItems"),
                        Field($"{branchPrefix}.mods.enabled-mods", "Enabled Mod IDs", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "Mods"),
                        Field($"{branchPrefix}.mods.map-folders", "Map Folders", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "Map"),
                    }),
            });
    }

    private static StructuredPageDefinition BuildNetworkPage(string branchPrefix)
    {
        return new StructuredPageDefinition(
            $"{branchPrefix}.network-and-admin",
            "Network & Admin",
            new[]
            {
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.access",
                    "Access & Passwords",
                    new[]
                    {
                        Field($"{branchPrefix}.network.bind-ip", "Bind IP", StructuredValueKind.Text, ConfigFileKind.Ini, "BindIP", helpText: "Leave empty to bind normally, or pin the server to a specific interface."),
                        Field($"{branchPrefix}.network.server-password", "Server Password", StructuredValueKind.Text, ConfigFileKind.Ini, "Password", helpText: "Optional join password for players. Leave blank to preserve the existing value."),
                        Field($"{branchPrefix}.network.rcon-password", "RCON Password", StructuredValueKind.Text, ConfigFileKind.Ini, "RCONPassword", helpText: "Optional password for remote console clients. Leave blank to preserve the existing value."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.compatibility",
                    "Compatibility & Discovery",
                    new[]
                    {
                        Field($"{branchPrefix}.network.auto-whitelist", "Auto Create Whitelist Users", StructuredValueKind.Boolean, ConfigFileKind.Ini, "AutoCreateUserInWhiteList", defaultValue: "false", helpText: "Automatically add first-time players instead of requiring manual adduser."),
                        Field($"{branchPrefix}.network.do-lua-checksum", "Enforce Lua Checksum", StructuredValueKind.Boolean, ConfigFileKind.Ini, "DoLuaChecksum", defaultValue: "true", helpText: "Kick players whose Lua files do not match the server."),
                        Field($"{branchPrefix}.network.upnp", "Enable UPnP", StructuredValueKind.Boolean, ConfigFileKind.Ini, "UPnP", defaultValue: "true", helpText: "Ask routers that support UPnP to open ports automatically."),
                        Field($"{branchPrefix}.network.ping-limit", "Ping Limit", StructuredValueKind.Integer, ConfigFileKind.Ini, "PingLimit", defaultValue: "250", helpText: "Players consistently above this ping can be kicked. Use 100 to disable the limit."),
                        Field($"{branchPrefix}.network.steam-vac", "Steam VAC", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SteamVAC", defaultValue: "true", helpText: "Enable Valve Anti-Cheat integration for Steam clients."),
                        Field($"{branchPrefix}.network.kick-fast-players", "Kick Fast Players", StructuredValueKind.Boolean, ConfigFileKind.Ini, "KickFastPlayers", defaultValue: "false", helpText: "Kick players whose movement looks too fast for the simulation."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.identity-and-safety",
                    "Identity & PvP Safety",
                    new[]
                    {
                        Field($"{branchPrefix}.network.display-user-name", "Display Username", StructuredValueKind.Boolean, ConfigFileKind.Ini, "DisplayUserName", defaultValue: "true", helpText: "Show a player's username over their character."),
                        Field($"{branchPrefix}.network.show-first-last-name", "Show First & Last Name", StructuredValueKind.Boolean, ConfigFileKind.Ini, "ShowFirstAndLastName", defaultValue: "false", helpText: "Show character first and last names instead of just usernames."),
                        Field($"{branchPrefix}.network.safety-system", "Safety System", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SafetySystem", defaultValue: "true", helpText: "Enable PvP safety toggles for players."),
                        Field($"{branchPrefix}.network.safety-toggle-timer", "Safety Toggle Timer", StructuredValueKind.Integer, ConfigFileKind.Ini, "SafetyToggleTimer", defaultValue: "100", helpText: "Seconds needed to turn the safety toggle on or off."),
                        Field($"{branchPrefix}.network.safety-cooldown-timer", "Safety Cooldown Timer", StructuredValueKind.Integer, ConfigFileKind.Ini, "SafetyCooldownTimer", defaultValue: "120", helpText: "Seconds before safety can be toggled again."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.voice",
                    "Voice Chat",
                    new[]
                    {
                        Field($"{branchPrefix}.network.voice-enabled", "Voice Chat Enabled", StructuredValueKind.Boolean, ConfigFileKind.Ini, "VoiceEnable", defaultValue: "true", helpText: "Allow voice chat on the server."),
                        Field($"{branchPrefix}.network.voice-3d", "3D Voice", StructuredValueKind.Boolean, ConfigFileKind.Ini, "Voice3D", defaultValue: "true", helpText: "Attenuate voice by distance and direction in-world."),
                        Field($"{branchPrefix}.network.voice-min-distance", "Voice Min Distance", StructuredValueKind.Integer, ConfigFileKind.Ini, "VoiceMinDistance", defaultValue: "10", helpText: "Distance in tiles before voice begins to attenuate."),
                        Field($"{branchPrefix}.network.voice-max-distance", "Voice Max Distance", StructuredValueKind.Integer, ConfigFileKind.Ini, "VoiceMaxDistance", defaultValue: "100", helpText: "Maximum voice range in tiles."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.bootstrap",
                    "Launcher Admin Bootstrap",
                    new[]
                    {
                        Field($"{branchPrefix}.network.admin-user", "Admin Username", StructuredValueKind.Text, ConfigFileKind.Ini, "AdminUsername", helpText: "Launcher-managed bootstrap admin account used on server start."),
                        Field($"{branchPrefix}.network.admin-password", "Admin Password", StructuredValueKind.Text, ConfigFileKind.Ini, "AdminPassword", helpText: "Write-only launcher-managed bootstrap admin password."),
                    }),
            });
    }

    private static StructuredFieldDefinition Field(
        string fieldId,
        string displayName,
        StructuredValueKind kind,
        ConfigFileKind fileKind,
        string keyPath,
        bool restartRequired = false,
        string? defaultValue = null,
        string? helpText = null) =>
        new(fieldId, displayName, kind, new StructuredConfigTarget(fileKind, keyPath), defaultValue, restartRequired, helpText);
}
