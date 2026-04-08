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
                    $"{branchPrefix}.general.server",
                    "Server",
                    new[]
                    {
                        Field($"{branchPrefix}.server.name", "Server Name", StructuredValueKind.Text, ConfigFileKind.Ini, "ServerName"),
                        Field($"{branchPrefix}.server.port", "Default Port", StructuredValueKind.Integer, ConfigFileKind.Ini, "DefaultPort"),
                        Field($"{branchPrefix}.server.udp-port", "UDP Port", StructuredValueKind.Integer, ConfigFileKind.Ini, "UDPPort"),
                        Field($"{branchPrefix}.server.rcon-port", "RCON Port", StructuredValueKind.Integer, ConfigFileKind.Ini, "RCONPort"),
                    }),
                new StructuredSectionDefinition(
                    $"{branchPrefix}.general.runtime",
                    "Runtime",
                    new[]
                    {
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
                        Field($"{branchPrefix}.mods.map-folders", "Map Folders", StructuredValueKind.MultiLineText, ConfigFileKind.Ini, "MapFolders"),
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
                    $"{branchPrefix}.network.connection",
                    "Connection",
                    new[]
                    {
                        Field($"{branchPrefix}.network.bind-ip", "Bind IP", StructuredValueKind.Text, ConfigFileKind.Ini, "BindIP"),
                        Field($"{branchPrefix}.network.admin-user", "Admin Username", StructuredValueKind.Text, ConfigFileKind.Ini, "AdminUsername"),
                        Field($"{branchPrefix}.network.admin-password", "Admin Password", StructuredValueKind.Text, ConfigFileKind.Ini, "AdminPassword"),
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
