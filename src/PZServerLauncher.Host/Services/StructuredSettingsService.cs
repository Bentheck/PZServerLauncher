using System.Security.Cryptography;
using System.Globalization;
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
    IIniDocumentService iniDocumentService,
    ISandboxVarsDocumentService sandboxVarsDocumentService,
    WorkshopPresetScannerService workshopPresetScannerService)
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
            ProfileWorkspacePageIds.General => BuildGeneralValueSet(profile, catalog, definition, pageId),
            ProfileWorkspacePageIds.Sandbox => BuildSandboxValueSet(profile, catalog, definition, pageId),
            ProfileWorkspacePageIds.ModsAndMaps => BuildModsAndMapsValueSet(profile, catalog, definition, pageId),
            ProfileWorkspacePageIds.NetworkAndAdmin => BuildNetworkValueSet(profile, catalog, definition, pageId),
            _ => BuildFallbackValueSet(catalog, pageId, SettingsUnavailableMessage),
        };
    }

    public SettingsValidationResultDto Validate(ServerProfile profile, string pageId, IReadOnlyDictionary<string, string?> values)
    {
        return pageId switch
        {
            ProfileWorkspacePageIds.General => ValidateGeneral(profile, pageId, values),
            ProfileWorkspacePageIds.Sandbox => ValidateSandbox(profile, pageId, values),
            ProfileWorkspacePageIds.ModsAndMaps => ValidateModsAndMaps(profile, pageId, values),
            ProfileWorkspacePageIds.NetworkAndAdmin => ValidateNetwork(profile, pageId, values),
            _ => BuildFallbackValidation(pageId, SettingsUnavailableMessage),
        };
    }

    public WorkshopPreset GetWorkshopPreset(ServerProfile profile)
    {
        var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
        if (!string.IsNullOrWhiteSpace(raw.Content))
        {
            var parsed = iniDocumentService.Parse(raw.Content);
            if (parsed.IsSupported)
            {
                var sourceValues = iniDocumentService.ReadValues(raw.Content, ["WorkshopItems", "Mods", "Map"]);
                return ReadWorkshopPreset(sourceValues, profile.WorkshopPreset);
            }
        }

        return profile.WorkshopPreset;
    }

    public async Task<WorkshopPreset> SaveWorkshopPresetAsync(
        ServerProfile profile,
        WorkshopPreset preset,
        CancellationToken cancellationToken = default)
    {
        var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
        if (!string.IsNullOrWhiteSpace(raw.Content))
        {
            var parsed = iniDocumentService.Parse(raw.Content);
            if (!parsed.IsSupported)
            {
                throw new InvalidOperationException(BuildIniFallbackReason(parsed));
            }
        }

        var normalizedPreset = workshopPresetScannerService.Scan(profile.InstallDirectory, preset).Preset;
        var iniValues = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["WorkshopItems"] = JoinIniList(normalizedPreset.WorkshopItemIds),
            ["Mods"] = JoinIniList(normalizedPreset.EnabledModIds),
            ["Map"] = JoinIniList(normalizedPreset.MapFolders),
        };

        var updatedContent = iniDocumentService.ApplyValues(raw.Content, iniValues);
        configFileService.WriteRawFile(profile, ConfigFileKind.Ini, raw.Sha256, updatedContent);

        var updated = await profileStore.UpsertAsync(profile with
        {
            WorkshopPreset = normalizedPreset,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);

        return updated.WorkshopPreset;
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
                    var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
                    if (!string.IsNullOrWhiteSpace(raw.Content))
                    {
                        var parsed = iniDocumentService.Parse(raw.Content);
                        if (!parsed.IsSupported)
                        {
                            var fallbackReason = BuildIniFallbackReason(parsed);
                            return new SettingsSaveResultDto(
                                BuildFallbackValueSet(catalog, pageId, fallbackReason),
                                BuildFallbackValidation(pageId, fallbackReason),
                                false);
                        }
                    }

                    var iniValues = new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["PublicName"] = NormalizeIniLineValue(values, $"{branchPrefix}.server.public-name"),
                        ["PublicDescription"] = NormalizeIniLineValue(values, $"{branchPrefix}.server.public-description"),
                        ["Public"] = ParseBool(values, $"{branchPrefix}.server.public").ToString().ToLowerInvariant(),
                        ["Open"] = ParseBool(values, $"{branchPrefix}.server.open").ToString().ToLowerInvariant(),
                        ["MaxPlayers"] = ParseInt(values, $"{branchPrefix}.server.max-players").ToString(),
                        ["PVP"] = ParseBool(values, $"{branchPrefix}.server.pvp").ToString().ToLowerInvariant(),
                        ["PauseEmpty"] = ParseBool(values, $"{branchPrefix}.server.pause-empty").ToString().ToLowerInvariant(),
                        ["GlobalChat"] = ParseBool(values, $"{branchPrefix}.server.global-chat").ToString().ToLowerInvariant(),
                        ["ServerWelcomeMessage"] = NormalizeWelcomeMessage(values, $"{branchPrefix}.server.welcome-message"),
                        ["SpawnItems"] = JoinCommaSeparatedList(ParseCommaSeparatedEditorList(values, $"{branchPrefix}.server.spawn-items")),
                        ["HoursForLootRespawn"] = ParseInt(values, $"{branchPrefix}.server.loot-respawn-hours").ToString(),
                        ["MaxItemsForLootRespawn"] = ParseInt(values, $"{branchPrefix}.server.loot-respawn-max-items").ToString(),
                        ["ConstructionPreventsLootRespawn"] = ParseBool(values, $"{branchPrefix}.server.construction-prevents-loot-respawn").ToString().ToLowerInvariant(),
                        ["PlayerRespawnWithSelf"] = ParseBool(values, $"{branchPrefix}.server.respawn-with-self").ToString().ToLowerInvariant(),
                        ["PlayerRespawnWithOther"] = ParseBool(values, $"{branchPrefix}.server.respawn-with-other").ToString().ToLowerInvariant(),
                        ["HoursForWorldItemRemoval"] = ParseDecimal(values, $"{branchPrefix}.server.world-item-removal-hours"),
                        ["WorldItemRemovalList"] = JoinCommaSeparatedList(ParseCommaSeparatedEditorList(values, $"{branchPrefix}.server.world-item-removal-list")),
                        ["SleepAllowed"] = ParseBool(values, $"{branchPrefix}.server.sleep-allowed").ToString().ToLowerInvariant(),
                        ["SleepNeeded"] = ParseBool(values, $"{branchPrefix}.server.sleep-needed").ToString().ToLowerInvariant(),
                        ["NoFire"] = ParseBool(values, $"{branchPrefix}.server.no-fire").ToString().ToLowerInvariant(),
                        ["AnnounceDeath"] = ParseBool(values, $"{branchPrefix}.server.announce-death").ToString().ToLowerInvariant(),
                        ["DropOffWhiteListAfterDeath"] = ParseBool(values, $"{branchPrefix}.server.drop-whitelist-on-death").ToString().ToLowerInvariant(),
                        ["AllowDestructionBySledgehammer"] = ParseBool(values, $"{branchPrefix}.server.allow-sledgehammer-destruction").ToString().ToLowerInvariant(),
                        ["PlayerSafehouse"] = ParseBool(values, $"{branchPrefix}.server.player-safehouse").ToString().ToLowerInvariant(),
                        ["AdminSafehouse"] = ParseBool(values, $"{branchPrefix}.server.admin-safehouse").ToString().ToLowerInvariant(),
                        ["SafehouseAllowTrepass"] = ParseBool(values, $"{branchPrefix}.server.safehouse-allow-trespass").ToString().ToLowerInvariant(),
                        ["SafehouseAllowFire"] = ParseBool(values, $"{branchPrefix}.server.safehouse-allow-fire").ToString().ToLowerInvariant(),
                        ["SafehouseAllowLoot"] = ParseBool(values, $"{branchPrefix}.server.safehouse-allow-loot").ToString().ToLowerInvariant(),
                        ["SafehouseAllowRespawn"] = ParseBool(values, $"{branchPrefix}.server.safehouse-allow-respawn").ToString().ToLowerInvariant(),
                        ["SafehouseAllowNonResidential"] = ParseBool(values, $"{branchPrefix}.server.safehouse-allow-non-residential").ToString().ToLowerInvariant(),
                        ["DisableSafehouseWhenPlayerConnected"] = ParseBool(values, $"{branchPrefix}.server.disable-safehouse-when-player-connected").ToString().ToLowerInvariant(),
                        ["DisableSafehouseWhenPlayerDisconnected"] = ParseBool(values, $"{branchPrefix}.server.disable-safehouse-when-player-disconnected").ToString().ToLowerInvariant(),
                        ["SafehouseDaySurvivedToClaim"] = ParseInt(values, $"{branchPrefix}.server.safehouse-days-to-claim").ToString(),
                        ["SafeHouseRemovalTime"] = ParseInt(values, $"{branchPrefix}.server.safehouse-removal-hours").ToString(),
                        ["Faction"] = ParseBool(values, $"{branchPrefix}.server.faction-enabled").ToString().ToLowerInvariant(),
                        ["FactionDaySurvivedToCreate"] = ParseInt(values, $"{branchPrefix}.server.faction-days-to-create").ToString(),
                        ["FactionPlayersRequiredForTag"] = ParseInt(values, $"{branchPrefix}.server.faction-players-for-tag").ToString(),
                        ["AllowTradeUI"] = ParseBool(values, $"{branchPrefix}.server.allow-trade-ui").ToString().ToLowerInvariant(),
                        ["DefaultPort"] = ParseInt(values, $"{branchPrefix}.server.port").ToString(),
                        ["RCONPort"] = ParseInt(values, $"{branchPrefix}.server.rcon-port").ToString(),
                    };

                    var updatedContent = iniDocumentService.ApplyValues(raw.Content, iniValues);
                    configFileService.WriteRawFile(profile, ConfigFileKind.Ini, raw.Sha256, updatedContent);

                    var updated = await profileStore.UpsertAsync(profile with
                    {
                        DefaultPort = ParseInt(values, $"{branchPrefix}.server.port"),
                        UdpPort = ParseInt(values, $"{branchPrefix}.server.udp-port"),
                        RconPort = ParseInt(values, $"{branchPrefix}.server.rcon-port"),
                        PreferredMemoryInGigabytes = ParseInt(values, $"{branchPrefix}.runtime.memory"),
                        StartWithHost = ParseBool(values, $"{branchPrefix}.runtime.start-with-host"),
                        AutoRestartOnCrash = ParseBool(values, $"{branchPrefix}.runtime.auto-restart"),
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    }, cancellationToken);

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
            case ProfileWorkspacePageIds.ModsAndMaps:
                {
                    var branchPrefix = GetBranchPrefix(profile.Branch);
                    var normalizedPreset = await SaveWorkshopPresetAsync(
                        profile,
                        new WorkshopPreset
                        {
                            WorkshopItemIds = ParseEditorList(values, $"{branchPrefix}.mods.workshop-items"),
                            EnabledModIds = ParseEditorList(values, $"{branchPrefix}.mods.enabled-mods"),
                            MapFolders = ParseEditorList(values, $"{branchPrefix}.mods.map-folders"),
                        },
                        cancellationToken);

                    var updated = await profileStore.GetAsync(profile.ProfileId, cancellationToken) ?? profile with { WorkshopPreset = normalizedPreset };
                    return new SettingsSaveResultDto(GetPage(updated, pageId), validation, true);
                }
            case ProfileWorkspacePageIds.NetworkAndAdmin:
                {
                    var branchPrefix = GetBranchPrefix(profile.Branch);
                    var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
                    if (!string.IsNullOrWhiteSpace(raw.Content))
                    {
                        var parsed = iniDocumentService.Parse(raw.Content);
                        if (!parsed.IsSupported)
                        {
                            var fallbackReason = BuildIniFallbackReason(parsed);
                            return new SettingsSaveResultDto(
                                BuildFallbackValueSet(catalog, pageId, fallbackReason),
                                BuildFallbackValidation(pageId, fallbackReason),
                                false);
                        }
                    }

                    var currentIniValues = iniDocumentService.ReadValues(raw.Content, ["Password", "RCONPassword"]);
                    var serverPassword = NormalizeWriteOnlySecret(values, $"{branchPrefix}.network.server-password", GetIniValueOrDefault(currentIniValues, "Password", null));
                    var rconPassword = NormalizeWriteOnlySecret(values, $"{branchPrefix}.network.rcon-password", GetIniValueOrDefault(currentIniValues, "RCONPassword", null));

                    var iniValues = new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["BindIP"] = NormalizeOptional(values, $"{branchPrefix}.network.bind-ip"),
                        ["Password"] = serverPassword,
                        ["RCONPassword"] = rconPassword,
                        ["AutoCreateUserInWhiteList"] = ParseBool(values, $"{branchPrefix}.network.auto-whitelist").ToString().ToLowerInvariant(),
                        ["DoLuaChecksum"] = ParseBool(values, $"{branchPrefix}.network.do-lua-checksum").ToString().ToLowerInvariant(),
                        ["UPnP"] = ParseBool(values, $"{branchPrefix}.network.upnp").ToString().ToLowerInvariant(),
                        ["PingLimit"] = ParseInt(values, $"{branchPrefix}.network.ping-limit").ToString(),
                        ["SteamVAC"] = ParseBool(values, $"{branchPrefix}.network.steam-vac").ToString().ToLowerInvariant(),
                        ["KickFastPlayers"] = ParseBool(values, $"{branchPrefix}.network.kick-fast-players").ToString().ToLowerInvariant(),
                        ["DenyLoginOnOverloadedServer"] = ParseBool(values, $"{branchPrefix}.network.deny-login-overloaded").ToString().ToLowerInvariant(),
                        ["ClientCommandFilter"] = NormalizeOptional(values, $"{branchPrefix}.network.client-command-filter"),
                        ["SaveWorldEveryMinutes"] = ParseInt(values, $"{branchPrefix}.network.save-world-every-minutes").ToString(),
                        ["PlayerSaveOnDamage"] = ParseBool(values, $"{branchPrefix}.network.player-save-on-damage").ToString().ToLowerInvariant(),
                        ["DisplayUserName"] = ParseBool(values, $"{branchPrefix}.network.display-user-name").ToString().ToLowerInvariant(),
                        ["ShowFirstAndLastName"] = ParseBool(values, $"{branchPrefix}.network.show-first-last-name").ToString().ToLowerInvariant(),
                        ["MouseOverToSeeDisplayName"] = ParseBool(values, $"{branchPrefix}.network.mouse-over-display-name").ToString().ToLowerInvariant(),
                        ["HidePlayersBehindYou"] = ParseBool(values, $"{branchPrefix}.network.hide-players-behind-you").ToString().ToLowerInvariant(),
                        ["PlayerBumpPlayer"] = ParseBool(values, $"{branchPrefix}.network.player-bump-player").ToString().ToLowerInvariant(),
                        ["MapRemotePlayerVisibility"] = ParseInt(values, $"{branchPrefix}.network.map-remote-player-visibility").ToString(),
                        ["UseTCPForMapTraffic"] = ParseBool(values, $"{branchPrefix}.network.use-tcp-for-map-traffic").ToString().ToLowerInvariant(),
                        ["SafetySystem"] = ParseBool(values, $"{branchPrefix}.network.safety-system").ToString().ToLowerInvariant(),
                        ["ShowSafety"] = ParseBool(values, $"{branchPrefix}.network.show-safety").ToString().ToLowerInvariant(),
                        ["SafetyToggleTimer"] = ParseInt(values, $"{branchPrefix}.network.safety-toggle-timer").ToString(),
                        ["SafetyCooldownTimer"] = ParseInt(values, $"{branchPrefix}.network.safety-cooldown-timer").ToString(),
                        ["MaxAccountsPerUser"] = ParseInt(values, $"{branchPrefix}.network.max-accounts-per-user").ToString(),
                        ["AllowNonAsciiUsername"] = ParseBool(values, $"{branchPrefix}.network.allow-non-ascii-username").ToString().ToLowerInvariant(),
                        ["Tag"] = NormalizeOptional(values, $"{branchPrefix}.network.server-tag"),
                        ["ResetID"] = ParseInt(values, $"{branchPrefix}.network.reset-id").ToString(),
                        ["VoiceEnable"] = ParseBool(values, $"{branchPrefix}.network.voice-enabled").ToString().ToLowerInvariant(),
                        ["Voice3D"] = ParseBool(values, $"{branchPrefix}.network.voice-3d").ToString().ToLowerInvariant(),
                        ["VoiceMinDistance"] = ParseInt(values, $"{branchPrefix}.network.voice-min-distance").ToString(),
                        ["VoiceMaxDistance"] = ParseInt(values, $"{branchPrefix}.network.voice-max-distance").ToString(),
                        ["MinutesPerPage"] = ParseInt(values, $"{branchPrefix}.network.minutes-per-page").ToString(),
                    };

                    var updatedContent = iniDocumentService.ApplyValues(raw.Content, iniValues);
                    configFileService.WriteRawFile(profile, ConfigFileKind.Ini, raw.Sha256, updatedContent);

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

    private SettingsValueSetDto BuildGeneralValueSet(
        ServerProfile profile,
        StructuredSettingsCatalog catalog,
        StructuredPageDefinition definition,
        string pageId)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
        if (!string.IsNullOrWhiteSpace(raw.Content))
        {
            var parsed = iniDocumentService.Parse(raw.Content);
            if (!parsed.IsSupported)
            {
                var fallbackReason = BuildIniFallbackReason(parsed);
                return BuildFallbackValueSet(catalog, pageId, fallbackReason);
            }
        }

        var sourceValues = iniDocumentService.ReadValues(
            raw.Content,
            definition.Sections
                .SelectMany(section => section.Fields)
                .Where(field => !IsGeneralProfileBackedField(field.FieldId))
                .Select(field => field.Target.KeyPath));

        foreach (var field in definition.Sections.SelectMany(section => section.Fields))
        {
            values[field.FieldId] = field.FieldId switch
            {
                var id when id.EndsWith(".server.udp-port", StringComparison.Ordinal) => profile.UdpPort.ToString(),
                var id when id.EndsWith(".runtime.memory", StringComparison.Ordinal) => profile.PreferredMemoryInGigabytes.ToString(),
                var id when id.EndsWith(".runtime.start-with-host", StringComparison.Ordinal) => profile.StartWithHost.ToString(),
                var id when id.EndsWith(".runtime.auto-restart", StringComparison.Ordinal) => profile.AutoRestartOnCrash.ToString(),
                var id when id.EndsWith(".server.spawn-items", StringComparison.Ordinal) => ExpandCommaSeparatedList(
                    GetIniValueOrDefault(sourceValues, field.Target.KeyPath, field.DefaultValue)),
                var id when id.EndsWith(".server.world-item-removal-list", StringComparison.Ordinal) => ExpandCommaSeparatedList(
                    GetIniValueOrDefault(sourceValues, field.Target.KeyPath, field.DefaultValue)),
                var id when id.EndsWith(".server.welcome-message", StringComparison.Ordinal) => ExpandWelcomeMessage(
                    GetIniValueOrDefault(sourceValues, field.Target.KeyPath, field.DefaultValue)),
                _ => GetIniValueOrDefault(sourceValues, field.Target.KeyPath, field.DefaultValue),
            };
        }

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

    private SettingsValueSetDto BuildModsAndMapsValueSet(
        ServerProfile profile,
        StructuredSettingsCatalog catalog,
        StructuredPageDefinition definition,
        string pageId)
    {
        var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
        if (!string.IsNullOrWhiteSpace(raw.Content))
        {
            var parsed = iniDocumentService.Parse(raw.Content);
            if (!parsed.IsSupported)
            {
                var fallbackReason = BuildIniFallbackReason(parsed);
                return BuildFallbackValueSet(catalog, pageId, fallbackReason);
            }
        }

        var sourceValues = iniDocumentService.ReadValues(
            raw.Content,
            definition.Sections
                .SelectMany(section => section.Fields)
                .Select(field => field.Target.KeyPath));
        var preset = ReadWorkshopPreset(sourceValues, profile.WorkshopPreset);

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in definition.Sections.SelectMany(section => section.Fields))
        {
            values[field.FieldId] = field.FieldId switch
            {
                var id when id.EndsWith(".mods.workshop-items", StringComparison.Ordinal) => JoinEditorList(preset.WorkshopItemIds),
                var id when id.EndsWith(".mods.enabled-mods", StringComparison.Ordinal) => JoinEditorList(preset.EnabledModIds),
                var id when id.EndsWith(".mods.map-folders", StringComparison.Ordinal) => JoinEditorList(preset.MapFolders),
                _ => field.DefaultValue,
            };
        }

        return new SettingsValueSetDto(
            catalog.CatalogId,
            catalog.CatalogVersion,
            pageId,
            values,
            ComputeSourceHash(values),
            false,
            null);
    }

    private SettingsValueSetDto BuildNetworkValueSet(
        ServerProfile profile,
        StructuredSettingsCatalog catalog,
        StructuredPageDefinition definition,
        string pageId)
    {
        var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
        if (!string.IsNullOrWhiteSpace(raw.Content))
        {
            var parsed = iniDocumentService.Parse(raw.Content);
            if (!parsed.IsSupported)
            {
                var fallbackReason = BuildIniFallbackReason(parsed);
                return BuildFallbackValueSet(catalog, pageId, fallbackReason);
            }
        }

        var sourceValues = iniDocumentService.ReadValues(
            raw.Content,
            definition.Sections
                .SelectMany(section => section.Fields)
                .Where(field => !IsNetworkProfileBackedField(field.FieldId))
                .Select(field => field.Target.KeyPath));

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in definition.Sections.SelectMany(section => section.Fields))
        {
            values[field.FieldId] = field.FieldId switch
            {
                var id when id.EndsWith(".network.bind-ip", StringComparison.Ordinal) => GetIniValueOrDefault(sourceValues, field.Target.KeyPath, profile.BindIp ?? field.DefaultValue),
                var id when id.EndsWith(".network.admin-user", StringComparison.Ordinal) => profile.AdminUsername ?? string.Empty,
                var id when id.EndsWith(".network.admin-password", StringComparison.Ordinal) => string.Empty,
                var id when id.EndsWith(".network.server-password", StringComparison.Ordinal) => string.Empty,
                var id when id.EndsWith(".network.rcon-password", StringComparison.Ordinal) => string.Empty,
                _ => GetIniValueOrDefault(sourceValues, field.Target.KeyPath, field.DefaultValue),
            };
        }

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

        ValidateRequiredString(values, $"{branchPrefix}.server.public-name", "Public server name is required.", fieldErrors);
        ValidatePort(values, $"{branchPrefix}.server.port", fieldErrors);
        ValidatePort(values, $"{branchPrefix}.server.udp-port", fieldErrors);
        ValidatePort(values, $"{branchPrefix}.server.rcon-port", fieldErrors);
        ValidatePositiveInteger(values, $"{branchPrefix}.server.max-players", "Max players must be a whole number greater than zero.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.public", "Public listing must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.open", "Open access must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.pvp", "PvP must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.pause-empty", "Pause when empty must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.global-chat", "Global chat must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.server.loot-respawn-hours", 0, "Loot respawn hours must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.server.loot-respawn-max-items", 0, "Loot respawn max items must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.construction-prevents-loot-respawn", "Construction loot-respawn protection must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.respawn-with-self", "Respawn at death location must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.respawn-with-other", "Respawn with split-screen partner must be true or false.", fieldErrors);
        ValidateMinimumDecimal(values, $"{branchPrefix}.server.world-item-removal-hours", 0m, "World item removal hours must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.sleep-allowed", "Sleep allowed must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.sleep-needed", "Sleep needed must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.no-fire", "Disable fire must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.announce-death", "Announce death must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.drop-whitelist-on-death", "Drop whitelist on death must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.allow-sledgehammer-destruction", "Allow sledgehammer destruction must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.player-safehouse", "Player safehouses must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.admin-safehouse", "Admin safehouses must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.safehouse-allow-trespass", "Allow trespass must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.safehouse-allow-fire", "Allow fire damage must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.safehouse-allow-loot", "Allow looting must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.safehouse-allow-respawn", "Allow respawn must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.safehouse-allow-non-residential", "Allow non-residential claiming must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.disable-safehouse-when-player-connected", "Disable safehouse while owner is connected must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.disable-safehouse-when-player-disconnected", "Disable safehouse while owner is disconnected must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.server.safehouse-days-to-claim", 0, "Days to claim a safehouse must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.server.safehouse-removal-hours", 0, "Safehouse removal time must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.faction-enabled", "Factions enabled must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.server.faction-days-to-create", 0, "Days to create a faction must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.server.faction-players-for-tag", 1, "Players required for a faction tag must be at least one.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.server.allow-trade-ui", "Allow trade UI must be true or false.", fieldErrors);
        ValidatePositiveInteger(values, $"{branchPrefix}.runtime.memory", "Memory must be a whole number greater than zero.", fieldErrors);
        ValidateMaximumLength(values, $"{branchPrefix}.server.public-description", 256, "Public description should stay under 256 characters for the server browser.", fieldErrors);

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
        ValidateMinimumDecimal(values, $"{branchPrefix}.sandbox.population-multiplier", 0m, "Population multiplier must be zero or greater.", fieldErrors);
        ValidateMinimumDecimal(values, $"{branchPrefix}.sandbox.population-start-multiplier", 0m, "Start population must be zero or greater.", fieldErrors);
        ValidateMinimumDecimal(values, $"{branchPrefix}.sandbox.population-peak-multiplier", 0m, "Peak population must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.population-peak-day", 0, "Peak day must be zero or greater.", fieldErrors);
        ValidateMinimumDecimal(values, $"{branchPrefix}.sandbox.respawn-hours", 0m, "Respawn hours must be zero or greater.", fieldErrors);
        ValidateMinimumDecimal(values, $"{branchPrefix}.sandbox.respawn-unseen-hours", 0m, "Respawn unseen hours must be zero or greater.", fieldErrors);
        ValidateMinimumDecimal(values, $"{branchPrefix}.sandbox.respawn-multiplier", 0m, "Respawn multiplier must be zero or greater.", fieldErrors);
        ValidateMinimumDecimal(values, $"{branchPrefix}.sandbox.redistribute-hours", 0m, "Redistribute hours must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.follow-sound-distance", 0, "Follow sound distance must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.rally-group-size", 0, "Rally group size must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.rally-travel-distance", 0, "Rally travel distance must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.rally-group-separation", 0, "Rally group separation must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.rally-group-radius", 0, "Rally group radius must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.water-shut-modifier", -1, "Water shutoff day must be -1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.electricity-shut-modifier", -1, "Electricity shutoff day must be -1 or greater.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.erosion-speed", 1, 5, "Erosion speed must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.loot-respawn", 1, 5, "Loot respawn must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.food-loot", 1, 5, "Food loot must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.weapon-loot", 1, 5, "Weapon loot must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.other-loot", 1, 5, "Other loot must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.temperature", 1, 5, "Temperature must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.rain", 1, 5, "Rain must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.alarm", 1, 6, "House alarm frequency must be between 1 and 6.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.locked-houses", 1, 6, "Locked houses must be between 1 and 6.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.farming", 1, 5, "Farming speed must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.stats-decrease", 1, 5, "Stats decrease must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.nature-abundance", 1, 5, "Nature abundance must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.food-rot-speed", 1, 5, "Food rot speed must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.fridge-factor", 1, 5, "Fridge factor must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.plant-resilience", 1, 5, "Plant resilience must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.plant-abundance", 1, 5, "Plant abundance must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.end-regen", 1, 5, "Endurance regeneration must be between 1 and 5.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.helicopter", 1, "Helicopter event frequency must be 1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.meta-event", 1, "Meta event frequency must be 1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.sleeping-event", 1, "Sleeping event frequency must be 1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.generator-spawning", 1, "Generator spawn rate must be 1 or greater.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-speed", 1, 4, "Zombie speed must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-strength", 1, 4, "Zombie strength must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-toughness", 1, 4, "Zombie toughness must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-transmission", 1, 4, "Transmission must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-mortality", 1, 7, "Mortality must be between 1 and 7.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-reanimate", 1, 5, "Reanimate time must be between 1 and 5.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-cognition", 1, 4, "Cognition must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-memory", 1, 4, "Memory must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-decomp", 1, 4, "Decomp must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-sight", 1, 4, "Sight must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-hearing", 1, 4, "Hearing must be between 1 and 4.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.zombie-lore-smell", 1, 3, "Smell must be between 1 and 3.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.zombie-lore-trigger-house-alarm", "Trigger house alarm must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.zombie-lore-thump-no-chasing", "Thump without chasing must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.zombie-lore-thump-on-construction", "Thump on construction must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.zombie-lore-drag-down", "Drag down must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.zombie-lore-fence-lunge", "Fence lunge must be true or false.", fieldErrors);
        ValidateRangedInteger(values, $"{branchPrefix}.sandbox.character-free-points", -100, 100, "Character free points must stay between -100 and 100.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.construction-bonus-points", 0, "Construction bonus points must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.multi-hit", "Multi-hit must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.allow-exterior-generator", "Allow exterior generator must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.bone-fracture", "Bone fracture must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.attack-block-movements", "Attack blocks movement must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.all-clothes-unlocked", "All clothes unlocked must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.vehicle-easy-use", "Vehicle easy use must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.player-damage-from-crash", "Player damage from crash must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.fire-spread", "Fire spread must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.hours-for-corpse-removal", -1, "Hours for corpse removal must be -1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.decaying-corpse-health-impact", 1, "Corpse health impact must be 1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.blood-level", 1, "Blood level must be 1 or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.sandbox.clothing-degradation", 1, "Clothing degradation must be 1 or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.starter-kit", "Starter kit must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.nutrition", "Nutrition must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.enable-snow-on-ground", "Snow on ground must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.sandbox.enable-vehicles", "Vehicles enabled must be true or false.", fieldErrors);

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

        ValidateBoolean(values, $"{branchPrefix}.network.auto-whitelist", "Auto whitelist must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.do-lua-checksum", "Lua checksum enforcement must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.upnp", "UPnP must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.ping-limit", 0, "Ping limit must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.steam-vac", "Steam VAC must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.kick-fast-players", "Kick fast players must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.deny-login-overloaded", "Deny login when overloaded must be true or false.", fieldErrors);
        ValidateMaximumLength(values, $"{branchPrefix}.network.client-command-filter", 256, "Client command filter must stay under 256 characters.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.save-world-every-minutes", 0, "Save world every minutes must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.player-save-on-damage", "Player save on damage must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.display-user-name", "Display username must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.show-first-last-name", "Show first and last name must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.mouse-over-display-name", "Mouse-over display names must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.hide-players-behind-you", "Hide players behind you must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.player-bump-player", "Player bump player must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.map-remote-player-visibility", 0, "Remote map player visibility must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.use-tcp-for-map-traffic", "Use TCP for map traffic must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.safety-system", "Safety system must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.show-safety", "Show safety icon must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.safety-toggle-timer", 0, "Safety toggle timer must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.safety-cooldown-timer", 0, "Safety cooldown timer must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.max-accounts-per-user", 0, "Max accounts per user must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.allow-non-ascii-username", "Allow non-ASCII usernames must be true or false.", fieldErrors);
        ValidateMaximumLength(values, $"{branchPrefix}.network.server-tag", 32, "Server tag must stay under 32 characters.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.reset-id", 0, "Reset ID must be zero or greater.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.voice-enabled", "Voice chat enabled must be true or false.", fieldErrors);
        ValidateBoolean(values, $"{branchPrefix}.network.voice-3d", "3D voice must be true or false.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.voice-min-distance", 0, "Voice minimum distance must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.voice-max-distance", 0, "Voice maximum distance must be zero or greater.", fieldErrors);
        ValidateMinimumInteger(values, $"{branchPrefix}.network.minutes-per-page", 0, "Minutes per page must be zero or greater.", fieldErrors);

        var voiceMinKey = $"{branchPrefix}.network.voice-min-distance";
        var voiceMaxKey = $"{branchPrefix}.network.voice-max-distance";
        if (TryParseInt(values, voiceMinKey, out var voiceMinDistance) &&
            TryParseInt(values, voiceMaxKey, out var voiceMaxDistance) &&
            voiceMaxDistance < voiceMinDistance)
        {
            fieldErrors[voiceMaxKey] = ["Voice maximum distance must be greater than or equal to the minimum distance."];
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

    private static SettingsValidationResultDto ValidateModsAndMaps(
        ServerProfile profile,
        string pageId,
        IReadOnlyDictionary<string, string?> values)
    {
        var branchPrefix = GetBranchPrefix(profile.Branch);
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        ValidateNoDuplicateListEntries(values, $"{branchPrefix}.mods.workshop-items", "Workshop item IDs", fieldErrors);
        ValidateNoDuplicateListEntries(values, $"{branchPrefix}.mods.enabled-mods", "Enabled mod IDs", fieldErrors);
        ValidateNoDuplicateListEntries(values, $"{branchPrefix}.mods.map-folders", "Map folders", fieldErrors);

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
        var supportsStructuredEditing = pageId is ProfileWorkspacePageIds.General or ProfileWorkspacePageIds.Sandbox or ProfileWorkspacePageIds.ModsAndMaps or ProfileWorkspacePageIds.NetworkAndAdmin;
        var supportsDrafts = pageId is ProfileWorkspacePageIds.General or ProfileWorkspacePageIds.Sandbox or ProfileWorkspacePageIds.ModsAndMaps;

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
            MapControlKind(definition),
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

    private static SettingsFieldControlKind MapControlKind(StructuredFieldDefinition definition)
    {
        if (definition.FieldId.Contains(".password", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(definition.Target.KeyPath, "Password", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(definition.Target.KeyPath, "RCONPassword", StringComparison.OrdinalIgnoreCase))
        {
            return SettingsFieldControlKind.Password;
        }

        return definition.ValueKind switch
        {
            StructuredValueKind.Integer => SettingsFieldControlKind.Numeric,
            StructuredValueKind.Boolean => SettingsFieldControlKind.Checkbox,
            StructuredValueKind.MultiLineText => SettingsFieldControlKind.MultiLineText,
            StructuredValueKind.Choice => SettingsFieldControlKind.Select,
            _ => SettingsFieldControlKind.TextBox,
        };
    }

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

    private static void ValidateMinimumDecimal(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        decimal minimum,
        string error,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var value) ||
            !decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum)
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

    private static void ValidateMaximumLength(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        int maximumLength,
        string error,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (values.TryGetValue(fieldId, out var value) &&
            value is not null &&
            value.Length > maximumLength)
        {
            fieldErrors[fieldId] = [error];
        }
    }

    private static void ValidateNoDuplicateListEntries(
        IReadOnlyDictionary<string, string?> values,
        string fieldId,
        string label,
        IDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        if (!values.TryGetValue(fieldId, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        var duplicates = ParseEditorList(rawValue)
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            fieldErrors[fieldId] = [$"{label} contains duplicate entries: {string.Join(", ", duplicates)}."];
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

    private static string ParseDecimal(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) &&
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : throw new InvalidOperationException($"Invalid decimal setting '{key}'.");

    private static bool TryParseInt(IReadOnlyDictionary<string, string?> values, string key, out int parsed)
    {
        parsed = 0;
        return values.TryGetValue(key, out var value) && int.TryParse(value, out parsed);
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid boolean setting '{key}'.");

    private static string? NormalizeOptional(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string? NormalizeIniLineValue(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value)
            ? value?.ReplaceLineEndings(" ").Trim()
            : null;

    private static string NormalizeWelcomeMessage(IReadOnlyDictionary<string, string?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .ReplaceLineEndings(" <LINE> ")
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string? NormalizeWriteOnlySecret(IReadOnlyDictionary<string, string?> values, string key, string? existingValue) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : existingValue;

    private static string JoinEditorList(IReadOnlyList<string> values) =>
        string.Join(Environment.NewLine, values);

    private static string JoinIniList(IReadOnlyList<string> values) =>
        string.Join(';', values);

    private static string JoinCommaSeparatedList(IReadOnlyList<string> values) =>
        string.Join(',', values);

    private static IReadOnlyList<string> ParseEditorList(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value)
            ? ParseEditorList(value)
            : [];

    private static IReadOnlyList<string> ParseCommaSeparatedEditorList(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value)
            ? ParseCommaSeparatedEditorList(value)
            : [];

    private static IReadOnlyList<string> ParseEditorList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(line => line.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static IReadOnlyList<string> ParseCommaSeparatedEditorList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .ReplaceLineEndings("\n")
            .Split(['\n', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string ExpandWelcomeMessage(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("<LINE>", Environment.NewLine, StringComparison.OrdinalIgnoreCase).Trim();

    private static string ExpandCommaSeparatedList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(Environment.NewLine, SplitIniList(value, allowCommaFallback: true));

    private static string? GetIniValueOrDefault(
        IReadOnlyDictionary<string, string?> values,
        string keyPath,
        string? defaultValue) =>
        values.TryGetValue(keyPath, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;

    private static WorkshopPreset ReadWorkshopPreset(
        IReadOnlyDictionary<string, string?> values,
        WorkshopPreset fallback) =>
        new()
        {
            WorkshopItemIds = ReadIniListOrDefault(values, "WorkshopItems", fallback.WorkshopItemIds, allowCommaFallback: true),
            EnabledModIds = ReadIniListOrDefault(values, "Mods", fallback.EnabledModIds, allowCommaFallback: true),
            MapFolders = ReadIniListOrDefault(values, "Map", fallback.MapFolders, allowCommaFallback: false),
        };

    private static IReadOnlyList<string> ReadIniListOrDefault(
        IReadOnlyDictionary<string, string?> values,
        string keyPath,
        IReadOnlyList<string> fallback,
        bool allowCommaFallback)
    {
        if (!values.TryGetValue(keyPath, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        return SplitIniList(rawValue, allowCommaFallback);
    }

    private static IReadOnlyList<string> SplitIniList(string rawValue, bool allowCommaFallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        if (rawValue.Contains(';'))
        {
            return rawValue.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        if (allowCommaFallback)
        {
            return rawValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        return [rawValue.Trim()];
    }

    private static bool IsGeneralProfileBackedField(string fieldId) =>
        fieldId.EndsWith(".server.udp-port", StringComparison.Ordinal) ||
        fieldId.EndsWith(".runtime.memory", StringComparison.Ordinal) ||
        fieldId.EndsWith(".runtime.start-with-host", StringComparison.Ordinal) ||
        fieldId.EndsWith(".runtime.auto-restart", StringComparison.Ordinal);

    private static bool IsNetworkProfileBackedField(string fieldId) =>
        fieldId.EndsWith(".network.admin-user", StringComparison.Ordinal) ||
        fieldId.EndsWith(".network.admin-password", StringComparison.Ordinal);

    private static string BuildIniFallbackReason(StructuredConfigDocument document)
    {
        if (document.Issues.Count == 0)
        {
            return "The Project Zomboid server INI could not be represented safely in the structured editor. Use Advanced Files instead.";
        }

        return string.Join(" ", document.Issues.Select(issue => issue.Message));
    }

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
