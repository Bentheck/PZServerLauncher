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
                    $"{branchPrefix}.sandbox.core",
                    "Core Sandbox",
                    new[]
                    {
                        Field($"{branchPrefix}.sandbox.placeholder", "Sandbox Placeholder", StructuredValueKind.Text, ConfigFileKind.SandboxVars, "SandboxVars"),
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
        bool restartRequired = false) =>
        new(fieldId, displayName, kind, new StructuredConfigTarget(fileKind, keyPath), null, restartRequired);
}
