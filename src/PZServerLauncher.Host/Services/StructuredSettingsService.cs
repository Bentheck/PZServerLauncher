using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Host.Services;

public sealed class StructuredSettingsService(
    ProfileStore profileStore,
    ConfigFileService configFileService,
    ISettingsCatalogResolver catalogResolver)
{
    private const string SettingsUnavailableMessage = "Structured editing for this page has not been implemented yet. Use Advanced Files for the raw editor.";

    public SettingsCatalogDto GetCatalog(ServerProfile profile)
    {
        var catalog = catalogResolver.Resolve(profile.Branch);
        return new SettingsCatalogDto(
            catalog.CatalogId,
            catalog.CatalogVersion,
            profile.Branch,
            catalog.Pages.Select(MapPage).ToArray());
    }

    public SettingsValueSetDto GetPage(ServerProfile profile, string pageId)
    {
        var catalog = catalogResolver.Resolve(profile.Branch);
        if (!string.Equals(pageId, ProfileWorkspacePageIds.General, StringComparison.Ordinal))
        {
            return BuildFallbackValueSet(catalog, pageId, SettingsUnavailableMessage);
        }

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var common = configFileService.GetCommonConfig(profile);
        var branchPrefix = GetBranchPrefix(profile.Branch);

        values[$"{branchPrefix}.server.name"] = common.ServerName;
        values[$"{branchPrefix}.server.port"] = common.DefaultPort.ToString();
        values[$"{branchPrefix}.server.udp-port"] = common.UdpPort.ToString();
        values[$"{branchPrefix}.server.rcon-port"] = common.RconPort.ToString();
        values[$"{branchPrefix}.runtime.memory"] = common.PreferredMemoryInGigabytes.ToString();
        values[$"{branchPrefix}.runtime.start-with-host"] = common.StartWithHost.ToString();
        values[$"{branchPrefix}.runtime.auto-restart"] = common.AutoRestartOnCrash.ToString();

        return new SettingsValueSetDto(
            catalog.CatalogId,
            catalog.CatalogVersion,
            pageId,
            values,
            ComputeSourceHash(values),
            false,
            null);
    }

    public SettingsValidationResultDto Validate(ServerProfile profile, string pageId, IReadOnlyDictionary<string, string?> values)
    {
        if (!string.Equals(pageId, ProfileWorkspacePageIds.General, StringComparison.Ordinal))
        {
            return BuildFallbackValidation(pageId, SettingsUnavailableMessage);
        }

        var branchPrefix = GetBranchPrefix(profile.Branch);
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        ValidateRequiredString(values, $"{branchPrefix}.server.name", "Server name is required.", fieldErrors);
        ValidatePort(values, $"{branchPrefix}.server.port", fieldErrors);
        ValidatePort(values, $"{branchPrefix}.server.udp-port", fieldErrors);
        ValidatePort(values, $"{branchPrefix}.server.rcon-port", fieldErrors);
        ValidatePositiveInteger(values, $"{branchPrefix}.runtime.memory", "Memory must be a whole number greater than zero.", fieldErrors);

        return new SettingsValidationResultDto(
            pageId,
            fieldErrors.Count == 0,
            fieldErrors,
            [],
            false,
            null);
    }

    public async Task<SettingsSaveResultDto> SaveAsync(
        ServerProfile profile,
        string pageId,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(profile, pageId, values);
        var catalog = catalogResolver.Resolve(profile.Branch);
        if (!validation.IsValid || validation.RequiresAdvancedFilesFallback)
        {
            return new SettingsSaveResultDto(
                new SettingsValueSetDto(
                    catalog.CatalogId,
                    catalog.CatalogVersion,
                    pageId,
                    new Dictionary<string, string?>(values, StringComparer.Ordinal),
                    ComputeSourceHash(values),
                    validation.RequiresAdvancedFilesFallback,
                    validation.FallbackReason),
                validation,
                false);
        }

        var branchPrefix = GetBranchPrefix(profile.Branch);
        var current = configFileService.GetCommonConfig(profile);
        var common = current with
        {
            ServerName = GetRequiredString(values, $"{branchPrefix}.server.name"),
            DefaultPort = ParseInt(values, $"{branchPrefix}.server.port"),
            UdpPort = ParseInt(values, $"{branchPrefix}.server.udp-port"),
            RconPort = ParseInt(values, $"{branchPrefix}.server.rcon-port"),
            PreferredMemoryInGigabytes = ParseInt(values, $"{branchPrefix}.runtime.memory"),
            StartWithHost = ParseBool(values, $"{branchPrefix}.runtime.start-with-host"),
            AutoRestartOnCrash = ParseBool(values, $"{branchPrefix}.runtime.auto-restart"),
        };

        var updated = await profileStore.UpsertAsync(configFileService.ApplyCommonConfig(profile, common), cancellationToken);
        return new SettingsSaveResultDto(GetPage(updated, pageId), validation, true);
    }

    private static SettingsPageDto MapPage(StructuredPageDefinition definition) =>
        new(
            MapPageId(definition.PageId),
            definition.DisplayName,
            BuildPageDescription(definition.PageId),
            string.Equals(MapPageId(definition.PageId), ProfileWorkspacePageIds.General, StringComparison.Ordinal),
            string.Equals(MapPageId(definition.PageId), ProfileWorkspacePageIds.General, StringComparison.Ordinal),
            definition.Sections.Select(MapSection).ToArray());

    private static SettingsSectionDto MapSection(StructuredSectionDefinition definition) =>
        new(
            definition.SectionId,
            definition.DisplayName,
            null,
            definition.Fields.Select(MapField).ToArray());

    private static SettingsFieldDto MapField(StructuredFieldDefinition definition) =>
        new(
            definition.FieldId,
            definition.DisplayName,
            definition.Target.KeyPath,
            definition.Target.FileKind,
            MapControlKind(definition.ValueKind),
            MapValueKind(definition.ValueKind),
            definition.DefaultValue,
            definition.HelpText,
            definition.RestartRequired,
            false,
            []);

    private static SettingsValueSetDto BuildFallbackValueSet(StructuredSettingsCatalog catalog, string pageId, string message) =>
        new(
            catalog.CatalogId,
            catalog.CatalogVersion,
            pageId,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            null,
            true,
            message);

    private static SettingsValidationResultDto BuildFallbackValidation(string pageId, string message) =>
        new(
            pageId,
            false,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            [message],
            true,
            message);

    private static string MapPageId(string rawPageId) =>
        rawPageId.EndsWith(".general", StringComparison.Ordinal)
            ? ProfileWorkspacePageIds.General
            : rawPageId.EndsWith(".sandbox", StringComparison.Ordinal)
                ? ProfileWorkspacePageIds.Sandbox
                : rawPageId.EndsWith(".mods-and-maps", StringComparison.Ordinal)
                    ? ProfileWorkspacePageIds.ModsAndMaps
                    : rawPageId.EndsWith(".network-and-admin", StringComparison.Ordinal)
                        ? ProfileWorkspacePageIds.NetworkAndAdmin
                        : rawPageId.EndsWith(".advanced-files", StringComparison.Ordinal)
                            ? ProfileWorkspacePageIds.AdvancedFiles
                            : rawPageId;

    private static string BuildPageDescription(string rawPageId) =>
        MapPageId(rawPageId) switch
        {
            ProfileWorkspacePageIds.General => "Primary server identity, ports, startup, and memory controls.",
            ProfileWorkspacePageIds.Sandbox => "Branch-specific gameplay and world settings.",
            ProfileWorkspacePageIds.ModsAndMaps => "Workshop, mods, map ordering, and validation.",
            ProfileWorkspacePageIds.NetworkAndAdmin => "Bind address, admin credentials, and network-facing controls.",
            ProfileWorkspacePageIds.AdvancedFiles => "Raw config editing for unsupported or advanced scenarios.",
            _ => "Structured settings page.",
        };

    private static SettingsFieldControlKind MapControlKind(StructuredValueKind valueKind) =>
        valueKind switch
        {
            StructuredValueKind.Integer => SettingsFieldControlKind.Numeric,
            StructuredValueKind.Boolean => SettingsFieldControlKind.Checkbox,
            StructuredValueKind.MultiLineText => SettingsFieldControlKind.MultiLineText,
            StructuredValueKind.Choice => SettingsFieldControlKind.Select,
            _ => SettingsFieldControlKind.TextBox,
        };

    private static SettingsValueKind MapValueKind(StructuredValueKind valueKind) =>
        valueKind switch
        {
            StructuredValueKind.Integer => SettingsValueKind.Integer,
            StructuredValueKind.Boolean => SettingsValueKind.Boolean,
            StructuredValueKind.MultiLineText => SettingsValueKind.List,
            _ => SettingsValueKind.String,
        };

    private static string GetBranchPrefix(ProjectZomboidBranch branch) =>
        branch switch
        {
            ProjectZomboidBranch.Stable41 => "b41",
            ProjectZomboidBranch.Unstable42 => "b42",
            _ => "pz",
        };

    private static void ValidateRequiredString(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        string error,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var value) || string.IsNullOrWhiteSpace(value))
        {
            fieldErrors[fieldId] = [error];
        }
    }

    private static void ValidatePort(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var value) || !int.TryParse(value, out var port) || port is < 1 or > 65535)
        {
            fieldErrors[fieldId] = ["Port must be a whole number between 1 and 65535."];
        }
    }

    private static void ValidatePositiveInteger(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        string error,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var value) || !int.TryParse(value, out var parsed) || parsed <= 0)
        {
            fieldErrors[fieldId] = [error];
        }
    }

    private static string GetRequiredString(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Missing required setting '{key}'.");

    private static int ParseInt(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid integer setting '{key}'.");

    private static bool ParseBool(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid boolean setting '{key}'.");

    private static string ComputeSourceHash(IReadOnlyDictionary<string, string?> values)
    {
        var ordered = values
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(ordered);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}
