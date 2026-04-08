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
                    $"{branchPrefix}.general.spawn-and-loot",
                    "Spawn & Loot Lifecycle",
                    new[]
                    {
                        Field($"{branchPrefix}.server.spawn-items", "Spawn Items", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "SpawnItems", helpText: "One item per line. The launcher writes them back as a comma-separated Project Zomboid SpawnItems list."),
                        Field($"{branchPrefix}.server.loot-respawn-hours", "Loot Respawn Hours", StructuredValueKind.Integer, ConfigFileKind.Ini, "HoursForLootRespawn", defaultValue: "0", helpText: "In-game hours before loot can respawn. Use 0 to disable loot respawn."),
                        Field($"{branchPrefix}.server.loot-respawn-max-items", "Loot Respawn Max Items", StructuredValueKind.Integer, ConfigFileKind.Ini, "MaxItemsForLootRespawn", defaultValue: "4", helpText: "Containers respawn loot only when they contain this many items or fewer."),
                        Field($"{branchPrefix}.server.construction-prevents-loot-respawn", "Construction Blocks Loot Respawn", StructuredValueKind.Boolean, ConfigFileKind.Ini, "ConstructionPreventsLootRespawn", defaultValue: "true", helpText: "Prevent loot respawn in containers near player construction."),
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
                        Field($"{branchPrefix}.server.drop-whitelist-on-death", "Drop Whitelist On Death", StructuredValueKind.Boolean, ConfigFileKind.Ini, "DropOffWhiteListAfterDeath", defaultValue: "false", helpText: "Remove a player's whitelist access when their current character dies."),
                        Field($"{branchPrefix}.server.allow-sledgehammer-destruction", "Allow Sledgehammer Destruction", StructuredValueKind.Boolean, ConfigFileKind.Ini, "AllowDestructionBySledgehammer", defaultValue: "true", helpText: "Allow survivors to destroy world objects with a sledgehammer."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.respawn-and-cleanup",
                    "Respawn & Cleanup",
                    new[]
                    {
                        Field($"{branchPrefix}.server.respawn-with-self", "Respawn At Death Location", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PlayerRespawnWithSelf", defaultValue: "false", helpText: "Let players respawn at their own place of death and keep playing in the same world."),
                        Field($"{branchPrefix}.server.respawn-with-other", "Respawn With Split-Screen Partner", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PlayerRespawnWithOther", defaultValue: "false", helpText: "Allow spawning at a split-screen partner's location."),
                        Field($"{branchPrefix}.server.world-item-removal-hours", "World Item Removal Hours", StructuredValueKind.Text, ConfigFileKind.Ini, "HoursForWorldItemRemoval", defaultValue: "0.0", helpText: "In-game hours before configured ground items are cleaned up. Use 0 to disable removal."),
                        Field($"{branchPrefix}.server.world-item-removal-list", "World Item Removal List", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "WorldItemRemovalList", helpText: "One item per line. The launcher writes them back as a comma-separated Project Zomboid cleanup list."),
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
                    $"{branchPrefix}.sandbox.zombie-population",
                    "Zombie Population",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.population-multiplier", "Population Multiplier", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "ZombieConfig.PopulationMultiplier", defaultValue: "1.0", helpText: "Global zombie population multiplier across the map."),
                        Field($"{branchPrefix}.sandbox.population-start-multiplier", "Start Population", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "ZombieConfig.PopulationStartMultiplier", defaultValue: "1.0", helpText: "Population multiplier on day one."),
                        Field($"{branchPrefix}.sandbox.population-peak-multiplier", "Peak Population", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "ZombieConfig.PopulationPeakMultiplier", defaultValue: "1.5", helpText: "Population multiplier once the world reaches peak intensity."),
                        Field($"{branchPrefix}.sandbox.population-peak-day", "Peak Day", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieConfig.PopulationPeakDay", defaultValue: "28", helpText: "Days after apocalypse start before peak population is reached."),
                        Field($"{branchPrefix}.sandbox.respawn-hours", "Respawn Hours", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "ZombieConfig.RespawnHours", defaultValue: "72.0", helpText: "Hours before cleared zones begin to respawn zombies."),
                        Field($"{branchPrefix}.sandbox.respawn-unseen-hours", "Respawn Unseen Hours", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "ZombieConfig.RespawnUnseenHours", defaultValue: "16.0", helpText: "Hours a cell must remain unseen before respawn can happen."),
                        Field($"{branchPrefix}.sandbox.respawn-multiplier", "Respawn Multiplier", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "ZombieConfig.RespawnMultiplier", defaultValue: "0.1", helpText: "Share of missing population restored on each respawn pass."),
                        Field($"{branchPrefix}.sandbox.redistribute-hours", "Redistribute Hours", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "ZombieConfig.RedistributeHours", defaultValue: "12.0", helpText: "Hours between zombie redistribution passes."),
                        Field($"{branchPrefix}.sandbox.follow-sound-distance", "Follow Sound Distance", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieConfig.FollowSoundDistance", defaultValue: "100", helpText: "How far zombies follow a sound source before giving up."),
                        Field($"{branchPrefix}.sandbox.rally-group-size", "Rally Group Size", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieConfig.RallyGroupSize", defaultValue: "20", helpText: "Maximum number of zombies in one rally group."),
                        Field($"{branchPrefix}.sandbox.rally-travel-distance", "Rally Travel Distance", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieConfig.RallyTravelDistance", defaultValue: "20", helpText: "How far rally groups move when redistributing."),
                        Field($"{branchPrefix}.sandbox.rally-group-separation", "Rally Group Separation", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieConfig.RallyGroupSeparation", defaultValue: "15", helpText: "Distance kept between neighboring rally groups."),
                        Field($"{branchPrefix}.sandbox.rally-group-radius", "Rally Group Radius", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieConfig.RallyGroupRadius", defaultValue: "3", helpText: "Radius occupied by a rally group once it gathers."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.zombie-lore",
                    "Zombie Lore",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.zombie-lore-speed", "Zombie Speed", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Speed", defaultValue: "2", helpText: "Project Zomboid zombie speed preset value."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-strength", "Zombie Strength", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Strength", defaultValue: "2", helpText: "Project Zomboid zombie strength preset value."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-toughness", "Zombie Toughness", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Toughness", defaultValue: "2", helpText: "Project Zomboid zombie toughness preset value."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-transmission", "Infection Transmission", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Transmission", defaultValue: "1", helpText: "How bites and scratches transmit the Knox infection."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-mortality", "Infection Mortality", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Mortality", defaultValue: "5", helpText: "How quickly the infection becomes fatal."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-reanimate", "Reanimate Delay", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Reanimate", defaultValue: "2", helpText: "How quickly dead survivors reanimate."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-cognition", "Zombie Cognition", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Cognition", defaultValue: "3", helpText: "How well zombies interact with doors and obstacles."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-memory", "Zombie Memory", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Memory", defaultValue: "2", helpText: "How long zombies remember their target."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-decomp", "Zombie Decomp", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Decomp", defaultValue: "1", helpText: "How much decomposition slows and weakens zombies over time."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-sight", "Zombie Sight", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Sight", defaultValue: "2", helpText: "How quickly zombies spot survivors visually."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-hearing", "Zombie Hearing", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Hearing", defaultValue: "2", helpText: "How sensitive zombies are to sound cues."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-smell", "Zombie Smell", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ZombieLore.Smell", defaultValue: "2", helpText: "How effectively zombies track survivors by scent."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-trigger-house-alarm", "Trigger House Alarm", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "ZombieLore.TriggerHouseAlarm", defaultValue: "false", helpText: "Allow zombies to trigger house alarms while moving through homes."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-thump-no-chasing", "Thump Without Chasing", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "ZombieLore.ThumpNoChasing", defaultValue: "false", helpText: "Allow zombies to thump doors and windows even when they are not actively chasing players."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-thump-on-construction", "Thump On Construction", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "ZombieLore.ThumpOnConstruction", defaultValue: "true", helpText: "Allow zombies to attack player-built constructions."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-drag-down", "Drag Down", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "ZombieLore.ZombiesDragDown", defaultValue: "true", helpText: "Allow groups of zombies to drag survivors to the ground."),
                        Field($"{branchPrefix}.sandbox.zombie-lore-fence-lunge", "Fence Lunge", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "ZombieLore.ZombiesFenceLunge", defaultValue: "true", helpText: "Allow zombies to lunge at survivors when climbing fences."),
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
                    $"{branchPrefix}.sandbox.world-events",
                    "World Events",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.helicopter", "Helicopter Event", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "Helicopter", defaultValue: "2", helpText: "Controls how often the helicopter story event appears."),
                        Field($"{branchPrefix}.sandbox.meta-event", "Meta Event Frequency", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "MetaEvent", defaultValue: "1", helpText: "Ambient events like distant screams or gunshots."),
                        Field($"{branchPrefix}.sandbox.sleeping-event", "Sleeping Event Frequency", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "SleepingEvent", defaultValue: "1", helpText: "How often sleeping survivors are interrupted by world events."),
                        Field($"{branchPrefix}.sandbox.generator-spawning", "Generator Spawn Rate", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "GeneratorSpawning", defaultValue: "3", helpText: "How often generators appear in the world."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.survivor-boosts",
                    "Survivor Boosts",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.character-free-points", "Character Free Points", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "CharacterFreePoints", defaultValue: "0", helpText: "Free trait points granted during character creation."),
                        Field($"{branchPrefix}.sandbox.construction-bonus-points", "Construction Bonus Points", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ConstructionBonusPoints", defaultValue: "3", helpText: "Bonus points available in the construction menu."),
                        Field($"{branchPrefix}.sandbox.multi-hit", "Multi-Hit", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "MultiHit", defaultValue: "false", helpText: "Allow melee weapons to strike multiple zombies."),
                        Field($"{branchPrefix}.sandbox.allow-exterior-generator", "Allow Exterior Generator", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "AllowExteriorGenerator", defaultValue: "false", helpText: "Permit generators to run while placed outside."),
                        Field($"{branchPrefix}.sandbox.bone-fracture", "Bone Fracture", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "BoneFracture", defaultValue: "true", helpText: "Allow serious injuries to cause broken bones."),
                        Field($"{branchPrefix}.sandbox.attack-block-movements", "Attack Blocks Movement", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "AttackBlockMovements", defaultValue: "true", helpText: "Let attack animations interrupt movement while fighting."),
                        Field($"{branchPrefix}.sandbox.all-clothes-unlocked", "All Clothes Unlocked", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "AllClothesUnlocked", defaultValue: "false", helpText: "Unlock all clothing choices in the character creator."),
                        Field($"{branchPrefix}.sandbox.vehicle-easy-use", "Vehicle Easy Use", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "VehicleEasyUse", defaultValue: "false", helpText: "Relax the normal restrictions around using vehicles."),
                        Field($"{branchPrefix}.sandbox.player-damage-from-crash", "Player Damage From Crash", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "PlayerDamageFromCrash", defaultValue: "true", helpText: "Allow vehicle crashes to injure the player."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.cleanup-and-wear",
                    "Cleanup & Wear",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.fire-spread", "Fire Spread", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "FireSpread", defaultValue: "true", helpText: "Allow fires to spread through the world."),
                        Field($"{branchPrefix}.sandbox.hours-for-corpse-removal", "Hours For Corpse Removal", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "HoursForCorpseRemoval", defaultValue: "216", helpText: "How long corpses remain before automatic cleanup."),
                        Field($"{branchPrefix}.sandbox.decaying-corpse-health-impact", "Corpse Health Impact", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "DecayingCorpseHealthImpact", defaultValue: "2", helpText: "How much nearby corpses affect survivor health."),
                        Field($"{branchPrefix}.sandbox.blood-level", "Blood Level", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "BloodLevel", defaultValue: "3", helpText: "How much blood remains in the world over time."),
                        Field($"{branchPrefix}.sandbox.clothing-degradation", "Clothing Degradation", StructuredValueKind.Integer, ConfigFileKind.SandboxVars, "ClothingDegradation", defaultValue: "3", helpText: "How quickly clothing wears out during play."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.sandbox.player-experience",
                    "Player Experience",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.starter-kit", "Starter Kit", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "StarterKit", defaultValue: "false", helpText: "Enable the beginner starter kit."),
                        Field($"{branchPrefix}.sandbox.nutrition", "Nutrition", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "Nutrition", defaultValue: "false", helpText: "Track calories, weight, and nutrition in multiplayer."),
                        Field($"{branchPrefix}.sandbox.enable-snow-on-ground", "Snow On Ground", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "EnableSnowOnGround", defaultValue: "true", helpText: "Render persistent snow coverage when winter conditions allow."),
                        Field($"{branchPrefix}.sandbox.enable-vehicles", "Vehicles Enabled", StructuredValueKind.Boolean, ConfigFileKind.SandboxVars, "EnableVehicles", defaultValue: "true", helpText: "Allow vehicles to exist and function in the world."),
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
                        Field($"{branchPrefix}.network.deny-login-overloaded", "Deny Login When Overloaded", StructuredValueKind.Boolean, ConfigFileKind.Ini, "DenyLoginOnOverloadedServer", defaultValue: "true", helpText: "Reject new logins while the server is under heavy load."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.identity-and-safety",
                    "Identity & PvP Safety",
                    new[]
                    {
                        Field($"{branchPrefix}.network.display-user-name", "Display Username", StructuredValueKind.Boolean, ConfigFileKind.Ini, "DisplayUserName", defaultValue: "true", helpText: "Show a player's username over their character."),
                        Field($"{branchPrefix}.network.show-first-last-name", "Show First & Last Name", StructuredValueKind.Boolean, ConfigFileKind.Ini, "ShowFirstAndLastName", defaultValue: "false", helpText: "Show character first and last names instead of just usernames."),
                        Field($"{branchPrefix}.network.safety-system", "Safety System", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SafetySystem", defaultValue: "true", helpText: "Enable PvP safety toggles for players."),
                        Field($"{branchPrefix}.network.show-safety", "Show Safety Icon", StructuredValueKind.Boolean, ConfigFileKind.Ini, "ShowSafety", defaultValue: "true", helpText: "Show the PvP safety skull icon above players when safety is disabled."),
                        Field($"{branchPrefix}.network.safety-toggle-timer", "Safety Toggle Timer", StructuredValueKind.Integer, ConfigFileKind.Ini, "SafetyToggleTimer", defaultValue: "100", helpText: "Seconds needed to turn the safety toggle on or off."),
                        Field($"{branchPrefix}.network.safety-cooldown-timer", "Safety Cooldown Timer", StructuredValueKind.Integer, ConfigFileKind.Ini, "SafetyCooldownTimer", defaultValue: "120", helpText: "Seconds before safety can be toggled again."),
                        Field($"{branchPrefix}.network.max-accounts-per-user", "Max Accounts Per User", StructuredValueKind.Integer, ConfigFileKind.Ini, "MaxAccountsPerUser", defaultValue: "0", helpText: "Limit how many different server accounts a single Steam user may create. Use 0 for unlimited."),
                        Field($"{branchPrefix}.network.allow-non-ascii-username", "Allow Non-ASCII Usernames", StructuredValueKind.Boolean, ConfigFileKind.Ini, "AllowNonAsciiUsername", defaultValue: "false", helpText: "Permit usernames that contain non-ASCII characters."),
                        Field($"{branchPrefix}.network.player-save-on-damage", "Save Player On Damage", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PlayerSaveOnDamage", defaultValue: "true", helpText: "Persist character state when damage is taken to reduce rollback after crashes."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.player-presence",
                    "Player Presence",
                    new[]
                    {
                        Field($"{branchPrefix}.network.mouse-over-display-name", "Mouse Over To See Display Name", StructuredValueKind.Boolean, ConfigFileKind.Ini, "MouseOverToSeeDisplayName", defaultValue: "true", helpText: "Show player display names when hovered in the world."),
                        Field($"{branchPrefix}.network.hide-players-behind-you", "Hide Players Behind You", StructuredValueKind.Boolean, ConfigFileKind.Ini, "HidePlayersBehindYou", defaultValue: "true", helpText: "Hide player models when they are directly behind the camera to reduce clutter."),
                        Field($"{branchPrefix}.network.player-bump-player", "Player Bump Player", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PlayerBumpPlayer", defaultValue: "false", helpText: "Allow survivors to physically bump into one another."),
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
