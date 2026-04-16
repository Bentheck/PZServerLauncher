using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

public sealed class ProjectZomboidSettingsCatalogResolver : ISettingsCatalogResolver
{
    private static readonly StructuredSettingsCatalog Unstable42Catalog = BuildCatalog(
        catalogId: ProjectZomboidBranchSupport.CurrentCatalogId,
        version: ProjectZomboidBranchSupport.CurrentCatalogVersion,
        branch: ProjectZomboidBranchSupport.CurrentBranch,
        branchPrefix: ProjectZomboidBranchSupport.CurrentFieldPrefix);

    public StructuredSettingsCatalog Resolve(ProjectZomboidBranch branch) => Unstable42Catalog;

    private static StructuredSettingsCatalog BuildCatalog(string catalogId, int version, ProjectZomboidBranch branch, string branchPrefix)
    {
        var pages = new[]
        {
            BuildGeneralPage(branchPrefix),
            ProjectZomboidB42SandboxCatalog.BuildPage(branchPrefix),
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
                        Field($"{branchPrefix}.server.safehouse-allow-non-residential", "Allow Non-Residential Claiming", StructuredValueKind.Boolean, ConfigFileKind.Ini, "SafehouseAllowNonResidential", defaultValue: "false", helpText: "Allow players to claim warehouses, shops, and other non-residential buildings as safehouses."),
                        Field($"{branchPrefix}.server.disable-safehouse-when-player-connected", "Disable While Owner Connected", StructuredValueKind.Boolean, ConfigFileKind.Ini, "DisableSafehouseWhenPlayerConnected", defaultValue: "false", helpText: "Turn off safehouse protection while a member is currently online."),
                        Field($"{branchPrefix}.server.disable-safehouse-when-player-disconnected", "Disable While Owner Disconnected", StructuredValueKind.Boolean, ConfigFileKind.Ini, "DisableSafehouseWhenPlayerDisconnected", defaultValue: "false", helpText: "Turn off safehouse protection while all members are offline."),
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
                        Field($"{branchPrefix}.mods.enabled-mods", "Enabled Mod IDs", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "Mods", helpText: @"Enter the mod loading ID here. It can be found in \Steam\steamapps\workshop\modID\mods\modName\info.txt"),
                        Field($"{branchPrefix}.mods.workshop-items", "Workshop Item IDs", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "WorkshopItems", helpText: "List Workshop Mod IDs for the server to download. Separate each item with a semicolon when written back to the .ini."),
                        Field($"{branchPrefix}.mods.map-folders", "Map Folders", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "Map", helpText: @"Enter the folder name of the mod found in \Steam\steamapps\workshop\modID\mods\modName\media\maps\"),
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
                        Field($"{branchPrefix}.network.client-command-filter", "Client Command Filter", StructuredValueKind.Text, ConfigFileKind.Ini, "ClientCommandFilter", helpText: "Optional Lua client command allow/deny filter string."),
                        Field($"{branchPrefix}.network.save-world-every-minutes", "Save World Every Minutes", StructuredValueKind.Integer, ConfigFileKind.Ini, "SaveWorldEveryMinutes", defaultValue: "0", helpText: "How often the world is saved to disk. Use 0 to keep the current server default."),
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
                        Field($"{branchPrefix}.network.server-tag", "Server Tag", StructuredValueKind.Text, ConfigFileKind.Ini, "Tag", helpText: "Optional short tag shown with the server identity."),
                        Field($"{branchPrefix}.network.reset-id", "Reset ID", StructuredValueKind.Integer, ConfigFileKind.Ini, "ResetID", defaultValue: "0", helpText: "Project Zomboid reset marker used when resetting world/player state."),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.network.player-presence",
                    "Player Presence",
                    new[]
                    {
                        Field($"{branchPrefix}.network.mouse-over-display-name", "Mouse Over To See Display Name", StructuredValueKind.Boolean, ConfigFileKind.Ini, "MouseOverToSeeDisplayName", defaultValue: "true", helpText: "Show player display names when hovered in the world."),
                        Field($"{branchPrefix}.network.hide-players-behind-you", "Hide Players Behind You", StructuredValueKind.Boolean, ConfigFileKind.Ini, "HidePlayersBehindYou", defaultValue: "true", helpText: "Hide player models when they are directly behind the camera to reduce clutter."),
                        Field($"{branchPrefix}.network.player-bump-player", "Player Bump Player", StructuredValueKind.Boolean, ConfigFileKind.Ini, "PlayerBumpPlayer", defaultValue: "false", helpText: "Allow survivors to physically bump into one another."),
                        Field($"{branchPrefix}.network.map-remote-player-visibility", "Remote Map Player Visibility", StructuredValueKind.Integer, ConfigFileKind.Ini, "MapRemotePlayerVisibility", defaultValue: "1", helpText: "How much remote player position data is exposed on the world map."),
                        Field($"{branchPrefix}.network.use-tcp-for-map-traffic", "Use TCP For Map Traffic", StructuredValueKind.Boolean, ConfigFileKind.Ini, "UseTCPForMapTraffic", defaultValue: "false", helpText: "Send map traffic over TCP instead of the normal transport path."),
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
                        Field($"{branchPrefix}.network.minutes-per-page", "Minutes Per Page", StructuredValueKind.Integer, ConfigFileKind.Ini, "MinutesPerPage", defaultValue: "1", helpText: "How many in-game minutes survivors spend per page while reading."),
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
        string? helpText = null,
        IReadOnlyList<StructuredFieldOptionDefinition>? options = null) =>
        new(fieldId, displayName, kind, new StructuredConfigTarget(fileKind, keyPath), defaultValue, restartRequired, helpText, options);
}
