using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    public Task DeleteProfileAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                await services.GetRequiredService<ProfileRetirementService>()
                    .DeleteProfileAsync(profileId, "desktop", cancellationToken: cancellationToken);
                return 0;
            },
            cancellationToken);

    public Task<OperationResultDto?> UninstallServerAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var result = await services.GetRequiredService<ProfileRetirementService>()
                    .UninstallServerAsync(profileId, "desktop", cancellationToken: cancellationToken);
                return (OperationResultDto?)new OperationResultDto(true, result.Message);
            },
            cancellationToken);

    public Task<ProfileDto?> CreateStarterProfileAsync(CancellationToken cancellationToken = default) =>
        CreateStarterProfileAsync(
            "Main Server",
            ServerProfileFactory.DefaultStarterPort,
            ServerProfileFactory.DefaultPreferredMemoryInGigabytes,
            ServerProfileFactory.DefaultMaxPlayers,
            cancellationToken);

    public async Task<ProfileDto?> CreateStarterProfileAsync(
        string displayName,
        int defaultPort,
        CancellationToken cancellationToken = default)
    {
        return await CreateStarterProfileAsync(
            displayName,
            defaultPort,
            ServerProfileFactory.DefaultPreferredMemoryInGigabytes,
            ServerProfileFactory.DefaultMaxPlayers,
            cancellationToken);
    }

    public async Task<ProfileDto?> CreateStarterProfileAsync(
        string displayName,
        int defaultPort,
        int preferredMemoryInGigabytes,
        int maxPlayers,
        CancellationToken cancellationToken = default)
    {
        var existingProfiles = await ExecuteScopedAsync(
            async services => await services.GetRequiredService<ProfileStore>().ListAsync(cancellationToken),
            cancellationToken);
        var reservedPorts = existingProfiles
            .SelectMany(profile => new[] { profile.DefaultPort, profile.UdpPort, profile.RconPort })
            .ToArray();
        var availablePort = ServerProfileFactory.FindNextAvailableStarterPort(defaultPort, reservedPorts);
        var starter = ServerProfileFactory.CreateStarterProfile(
            displayName,
            availablePort,
            existingProfiles.Select(profile => profile.ProfileId),
            preferredMemoryInGigabytes: preferredMemoryInGigabytes);

        var request = new ProfileUpsertRequestDto(
            starter.ProfileId,
            starter.DisplayName,
            starter.ServerName,
            starter.InstallDirectory,
            starter.CacheDirectory,
            starter.Branch,
            starter.DefaultPort,
            starter.UdpPort,
            starter.RconPort,
            starter.UseSteam,
            starter.AdminUsername,
            starter.AdminPassword,
            starter.BindIp,
            starter.PreferredMemoryInGigabytes,
            starter.StartWithHost,
            starter.AutoRestartOnCrash,
            starter.WorkshopPreset,
            starter.BackupPolicy);

        await CreateProfileAsync(request, cancellationToken);
        return await ExecuteScopedAsync(
            async services =>
            {
                var store = services.GetRequiredService<ProfileStore>();
                var structuredSettingsService = services.GetRequiredService<StructuredSettingsService>();
                var createdProfile = await store.GetAsync(starter.ProfileId, cancellationToken)
                    ?? throw new InvalidOperationException("Profile creation completed, but the new profile could not be loaded.");
                var generalValues = BuildStarterGeneralValues(
                    structuredSettingsService.GetPage(createdProfile, ProfileWorkspacePageIds.General).Values,
                    createdProfile,
                    maxPlayers);
                var saveResult = await structuredSettingsService.SaveAsync(
                    createdProfile,
                    ProfileWorkspacePageIds.General,
                    generalValues,
                    cancellationToken);
                if (!saveResult.Validation.IsValid || saveResult.Validation.RequiresAdvancedFilesFallback || !saveResult.DraftUpdated)
                {
                    throw new InvalidOperationException("Profile creation completed, but the starter general settings could not be initialized.");
                }

                var updatedProfile = await store.GetAsync(starter.ProfileId, cancellationToken) ?? createdProfile;
                return updatedProfile.ToDto();
            },
            cancellationToken);
    }

    public Task CreateProfileAsync(ProfileUpsertRequestDto request, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var store = services.GetRequiredService<ProfileStore>();
                var model = request.ToModel();
                var portConflictMessage = await store.GetPortConflictMessageAsync(model, cancellationToken);
                if (portConflictMessage is not null)
                {
                    throw new InvalidOperationException(portConflictMessage);
                }

                var profile = await store.UpsertAsync(model, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.created",
                    profile.ProfileId,
                    "desktop",
                    $"Created profile {profile.DisplayName}.",
                    cancellationToken: cancellationToken);
                return 0;
            },
            cancellationToken);

    public Task<ProfileDto?> UpdateProfilePathsAsync(
        string profileId,
        string installDirectory,
        string cacheDirectory,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                if (string.IsNullOrWhiteSpace(installDirectory) || string.IsNullOrWhiteSpace(cacheDirectory))
                {
                    throw new InvalidOperationException("Install and cache directories are both required.");
                }

                var store = services.GetRequiredService<ProfileStore>();
                var profile = await store.GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var updated = await store.UpsertAsync(profile with
                {
                    InstallDirectory = installDirectory.Trim(),
                    CacheDirectory = cacheDirectory.Trim(),
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                }, cancellationToken);

                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.paths.updated",
                    updated.ProfileId,
                    "desktop",
                    $"Updated install and cache paths for {updated.DisplayName}.",
                    cancellationToken: cancellationToken);

                return updated.ToDto();
            },
            cancellationToken);

    public Task<List<ProfileImportCandidateDto>?> DiscoverLocalImportsAsync(CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services => (await services.GetRequiredService<LocalServerImportService>().DiscoverAsync(cancellationToken)).ToList(),
            cancellationToken);

    public Task<ProfileDto?> ImportLocalCandidateAsync(string candidateId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<LocalServerImportService>().ImportAsync(candidateId, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.imported",
                    profile.ProfileId,
                    "desktop",
                    $"Imported local server '{profile.ServerName}'.",
                    cancellationToken: cancellationToken);
                return profile.ToDto();
            },
            cancellationToken);

    public Task<CommonConfigDto?> UpdateCommonConfigAsync(
        string profileId,
        CommonConfigDto config,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var store = services.GetRequiredService<ProfileStore>();
                var profile = await store.GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var configFileService = services.GetRequiredService<ConfigFileService>();
                var updated = await store.UpsertAsync(configFileService.ApplyCommonConfig(profile, config), cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "config.common.updated",
                    profileId,
                    "desktop",
                    "Updated common profile config.",
                    cancellationToken: cancellationToken);
                return configFileService.GetCommonConfig(updated);
            },
            cancellationToken);

    private static IReadOnlyDictionary<string, string?> BuildStarterGeneralValues(
        IReadOnlyDictionary<string, string?> currentValues,
        ServerProfile profile,
        int maxPlayers)
    {
        var branchPrefix = ProjectZomboidBranchSupport.CurrentFieldPrefix;
        var validatedMaxPlayers = maxPlayers > 0
            ? maxPlayers
            : ServerProfileFactory.DefaultMaxPlayers;
        var values = new Dictionary<string, string?>(currentValues, StringComparer.Ordinal)
        {
            [$"{branchPrefix}.server.public-name"] = profile.DisplayName,
            [$"{branchPrefix}.server.max-players"] = validatedMaxPlayers.ToString(),
            [$"{branchPrefix}.server.port"] = profile.DefaultPort.ToString(),
            [$"{branchPrefix}.server.udp-port"] = profile.UdpPort.ToString(),
            [$"{branchPrefix}.server.rcon-port"] = profile.RconPort.ToString(),
            [$"{branchPrefix}.runtime.memory"] = profile.PreferredMemoryInGigabytes.ToString(),
            [$"{branchPrefix}.runtime.start-with-host"] = profile.StartWithHost.ToString(),
            [$"{branchPrefix}.runtime.auto-restart"] = profile.AutoRestartOnCrash.ToString(),
        };

        return values;
    }

    public Task<BackupPolicy?> GetBackupPolicyAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services => (await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken))?.BackupPolicy,
            cancellationToken);

    public Task<BackupPolicy?> UpdateBackupPolicyAsync(string profileId, BackupPolicy policy, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var store = services.GetRequiredService<ProfileStore>();
                var profile = await store.GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var updated = await store.UpsertAsync(profile with
                {
                    BackupPolicy = policy,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                }, cancellationToken);

                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.backup-policy.updated",
                    profileId,
                    "desktop",
                    "Updated backup retention and recovery policy.",
                    cancellationToken: cancellationToken);

                return updated.BackupPolicy;
            },
            cancellationToken);
}
