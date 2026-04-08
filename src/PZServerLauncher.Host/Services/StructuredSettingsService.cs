using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.Json;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Host.Services;

public sealed class StructuredSettingsService(
    ProfileStore profileStore,
    ConfigFileService configFileService,
    ISettingsCatalogResolver catalogResolver,
    ISandboxVarsDocumentService sandboxVarsDocumentService)
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
        var definition = ResolvePageDefinition(catalog, pageId);
        if (definition is null)
        {
            return BuildFallbackValueSet(catalog, pageId, SettingsUnavailableMessage);
        }

        return pageId switch
        {
            ProfileWorkspacePageIds.General => BuildGeneralValueSet(profile, catalog, pageId),
            ProfileWorkspacePageIds.Sandbox => BuildSandboxValueSet(profile, catalog, definition, pageId),
            ProfileWorkspacePageIds.NetworkAndAdmin => BuildNetworkValueSet(profile, catalog, pageId),
            _ => BuildFallbackValueSet(catalog, pageId, SettingsUnavailableMessage),
        };
    }

    public SettingsValidationResultDto Validate(ServerProfile profile, string pageId, IReadOnlyDictionary<string, string?> values)
    {
        return pageId switch
        {
            ProfileWorkspacePageIds.General => ValidateGeneral(profile, pageId, values),
            ProfileWorkspacePageIds.Sandbox => ValidateSandbox(profile, pageId, values),
            ProfileWorkspacePageIds.NetworkAndAdmin => ValidateNetwork(profile, pageId, values),
            _ => BuildFallbackValidation(pageId, SettingsUnavailableMessage),
        };
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

        switch (pageId)
        {
            case ProfileWorkspacePageIds.General:
                {
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
            case ProfileWorkspacePageIds.Sandbox:
                {
                    var definition = ResolvePageDefinition(catalog, pageId);
                    if (definition is null)
                    {
                        return new SettingsSaveResultDto(
                            BuildFallbackValueSet(catalog, pageId, SettingsUnavailableMessage),
                            BuildFallbackValidation(pageId, SettingsUnavailableMessage),
                            false);
                    }

                    var raw = configFileService.ReadRawFile(profile, ConfigFileKind.SandboxVars);
                    if (!string.IsNullOrWhiteSpace(raw.Content))
                    {
                        var parsed = sandboxVarsDocumentService.Parse(raw.Content);
                        if (!parsed.IsSupported)
                        {
                            var fallbackReason = BuildSandboxFallbackReason(parsed);
                            return new SettingsSaveResultDto(
                                BuildFallbackValueSet(catalog, pageId, fallbackReason),
                                BuildFallbackValidation(pageId, fallbackReason),
                                false);
                        }
                    }

                    var sandboxValues = definition.Sections
                        .SelectMany(section => section.Fields)
                        .ToDictionary(
                            field => field.Target.KeyPath,
                            field => values.TryGetValue(field.FieldId, out var value) ? value : field.DefaultValue,
                            StringComparer.Ordinal);

                    var updatedContent = sandboxVarsDocumentService.ApplyValues(raw.Content, sandboxValues);
                    configFileService.WriteRawFile(profile, ConfigFileKind.SandboxVars, raw.Sha256, updatedContent);
                    return new SettingsSaveResultDto(GetPage(profile, pageId), validation, true);
                }
            case ProfileWorkspacePageIds.NetworkAndAdmin:
                {
                    var branchPrefix = GetBranchPrefix(profile.Branch);
                    var updated = await profileStore.UpsertAsync(profile with
                    {
                        BindIp = NormalizeOptional(values, $"{branchPrefix}.network.bind-ip"),
                        AdminUsername = NormalizeOptional(values, $"{branchPrefix}.network.admin-user"),
                        AdminPassword = NormalizeWriteOnlySecret(values, $"{branchPrefix}.network.admin-password", profile.AdminPassword),
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    }, cancellationToken);

                    return new SettingsSaveResultDto(GetPage(updated, pageId), validation, true);
                }
            default:
                return new SettingsSaveResultDto(
                    BuildFallbackValueSet(catalog, pageId, SettingsUnavailableMessage),
                    BuildFallbackValidation(pageId, SettingsUnavailableMessage),
                    false);
        }
    }

    private SettingsValueSetDto BuildGeneralValueSet(ServerProfile profile, StructuredSettingsCatalog catalog, string pageId)
    {
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

    private SettingsValueSetDto BuildSandboxValueSet(
        ServerProfile profile,
        StructuredSettingsCatalog catalog,
        StructuredPageDefinition definition,
        string pageId)
    {
        var raw = configFileService.ReadRawFile(profile, ConfigFileKind.SandboxVars);
        if (string.IsNullOrWhiteSpace(raw.Content))
        {
            return new SettingsValueSetDto(
                catalog.CatalogId,
                catalog.CatalogVersion,
                pageId,
                BuildDefaultPageValues(definition),
                raw.Sha256,
                false,
                null);
        }

        var parsed = sandboxVarsDocumentService.Parse(raw.Content);
        if (!parsed.IsSupported)
        {
            var fallbackReason = BuildSandboxFallbackReason(parsed);
            return BuildFallbackValueSet(catalog, pageId, fallbackReason);
        }

        var sourceValues = sandboxVarsDocumentService.ReadValues(
            raw.Content,
            definition.Sections.SelectMany(section => section.Fields).Select(field => field.Target.KeyPath));

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in definition.Sections.SelectMany(section => section.Fields))
        {
            values[field.FieldId] = sourceValues.TryGetValue(field.Target.KeyPath, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : field.DefaultValue;
        }

        return new SettingsValueSetDto(
            catalog.CatalogId,
            catalog.CatalogVersion,
            pageId,
            values,
            raw.Sha256,
            false,
            null);
    }

    private SettingsValueSetDto BuildNetworkValueSet(ServerProfile profile, StructuredSettingsCatalog catalog, string pageId)
    {
        var branchPrefix = GetBranchPrefix(profile.Branch);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{branchPrefix}.network.bind-ip"] = profile.BindIp ?? string.Empty,
            [$"{branchPrefix}.network.admin-user"] = profile.AdminUsername ?? string.Empty,
            [$"{branchPrefix}.network.admin-password"] = string.Empty,
        };

        return new SettingsValueSetDto(
            catalog.CatalogId,
            catalog.CatalogVersion,
            pageId,
            values,
            ComputeSourceHash(values),
            false,
            null);
    }

    private static SettingsValidationResultDto ValidateGeneral(
        ServerProfile profile,
        string pageId,
        IReadOnlyDictionary<string, string?> values)
    {
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

    private static SettingsValidationResultDto ValidateSandbox(
        ServerProfile profile,
        string pageId,
        IReadOnlyDictionary<string, string?> values)
    {
        var branchPrefix = GetBranchPrefix(profile.Branch);
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombies", 1, 5, "Zombie spawn rate must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.distribution", 1, 2, "Zombie distribution must be 1 or 2.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.day-length", 1, 9, "Day length must be between 1 and 9.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.start-year", 1, 100, "Start year must be between 1 and 100.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.start-month", 1, 12, "Start month must be between 1 and 12.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.start-day", 1, 31, "Start day must be between 1 and 31.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.start-time", 1, 9, "Start time must be between 1 and 9.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.water-shut-modifier", -1, "Water shutoff day must be -1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.electricity-shut-modifier", -1, "Electricity shutoff day must be -1 or greater.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.food-loot", 1, 5, "Food loot must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.weapon-loot", 1, 5, "Weapon loot must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.other-loot", 1, 5, "Other loot must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.temperature", 1, 5, "Temperature must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.rain", 1, 5, "Rain must be between 1 and 5.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.starter-kit", "Starter kit must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.nutrition", "Nutrition must be true or false.", fieldErrors);

        return new SettingsValidationResultDto(
            pageId,
            fieldErrors.Count == 0,
            fieldErrors,
            [],
            false,
            null);
    }

    private static SettingsValidationResultDto ValidateNetwork(
        ServerProfile profile,
        string pageId,
        IReadOnlyDictionary<string, string?> values)
    {
        var branchPrefix = GetBranchPrefix(profile.Branch);
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var bindIpKey = $"{branchPrefix}.network.bind-ip";
        if (values.TryGetValue(bindIpKey, out var bindIp) &&
            !string.IsNullOrWhiteSpace(bindIp) &&
            !IPAddress.TryParse(bindIp, out _))
        {
            fieldErrors[bindIpKey] = ["Bind IP must be empty or a valid IPv4/IPv6 address."];
        }

        var passwordKey = $"{branchPrefix}.network.admin-password";
        var usernameKey = $"{branchPrefix}.network.admin-user";
        if (values.TryGetValue(passwordKey, out var password) &&
            !string.IsNullOrWhiteSpace(password) &&
            (!values.TryGetValue(usernameKey, out var username) || string.IsNullOrWhiteSpace(username)))
        {
            fieldErrors[usernameKey] = ["Admin username is required when setting a new admin password."];
        }

        return new SettingsValidationResultDto(
            pageId,
            fieldErrors.Count == 0,
            fieldErrors,
            [],
            false,
            null);
    }

    private static SettingsPageDto MapPage(StructuredPageDefinition definition)
    {
        var pageId = MapPageId(definition.PageId);
        var supportsStructuredEditing = pageId is ProfileWorkspacePageIds.General or ProfileWorkspacePageIds.Sandbox or ProfileWorkspacePageIds.NetworkAndAdmin;
        var supportsDrafts = pageId is ProfileWorkspacePageIds.General or ProfileWorkspacePageIds.Sandbox;

        return new SettingsPageDto(
            pageId,
            definition.DisplayName,
            BuildPageDescription(definition.PageId),
            supportsStructuredEditing,
            supportsDrafts,
            definition.Sections.Select(MapSection).ToArray());
    }

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

    private static StructuredPageDefinition? ResolvePageDefinition(StructuredSettingsCatalog catalog, string pageId) =>
        catalog.Pages.FirstOrDefault(definition => string.Equals(MapPageId(definition.PageId), pageId, StringComparison.Ordinal));

    private static IReadOnlyDictionary<string, string?> BuildDefaultPageValues(StructuredPageDefinition definition)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in definition.Sections.SelectMany(section => section.Fields))
        {
            values[field.FieldId] = field.DefaultValue;
        }

        return values;
    }

    private static string BuildSandboxFallbackReason(StructuredConfigDocument document)
    {
        if (document.Issues.Count == 0)
        {
            return "SandboxVars.lua could not be represented safely in the structured editor. Use Advanced Files instead.";
        }

        return string.Join(" ", document.Issues.Select(issue => issue.Message));
    }

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

    private static void ValidateRangedInteger(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        int minimum,
        int maximum,
        string error,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var value) || !int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum)
        {
            fieldErrors[fieldId] = [error];
        }
    }

    private static void ValidateMinimumInteger(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        int minimum,
        string error,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var value) || !int.TryParse(value, out var parsed) || parsed < minimum)
        {
            fieldErrors[fieldId] = [error];
        }
    }

    private static void ValidateBoolean(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        string error,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var value) || !bool.TryParse(value, out _))
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

    private static string? NormalizeOptional(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string? NormalizeWriteOnlySecret(IReadOnlyDictionary<string, string?> values, string key, string? existingValue) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : existingValue;

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
