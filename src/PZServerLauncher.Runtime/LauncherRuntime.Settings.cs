using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    public async Task<RuntimeSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var hostInfo = await BuildHostInfoAsync(cancellationToken);

        return await ExecuteScopedAsync(
            async services =>
            {
                var profileStore = services.GetRequiredService<ProfileStore>();
                var jobStore = services.GetRequiredService<JobStore>();
                var runtimeStateStore = services.GetRequiredService<RuntimeStateStore>();
                var backupService = services.GetRequiredService<ServerBackupService>();

                var profiles = await profileStore.ListAsync(cancellationToken);
                var statuses = profiles.ToDictionary(
                    profile => profile.ProfileId,
                    profile => runtimeStateStore.GetOrDefault(profile.ProfileId),
                    StringComparer.OrdinalIgnoreCase);
                var backups = profiles.ToDictionary(
                    profile => profile.ProfileId,
                    profile => (IReadOnlyList<string>)backupService.ListBackups(profile.ProfileId),
                    StringComparer.OrdinalIgnoreCase);
                var jobs = await jobStore.ListRecentAsync(20, cancellationToken);

                return new RuntimeSnapshot(
                    hostInfo,
                    profiles.Select(profile => profile.ToDto()).ToArray(),
                    statuses,
                    backups,
                    jobs);
            },
            cancellationToken);
    }

    public Task<WorkspaceBootstrapDto> GetWorkspaceBootstrapAsync(CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            services =>
            {
                var bootstrap = services.GetRequiredService<WorkspaceBootstrapService>().Build(BuildDesktopPrincipal());
                var filteredGlobalPages = bootstrap.GlobalPages
                    .Where(page => !string.Equals(page.Id, WorkspacePageIds.RemoteAccess, StringComparison.Ordinal) &&
                                   !string.Equals(page.Id, WorkspacePageIds.Users, StringComparison.Ordinal))
                    .ToArray();

                return Task.FromResult(new WorkspaceBootstrapDto(
                    bootstrap.Actor,
                    bootstrap.Capabilities,
                    filteredGlobalPages,
                    bootstrap.ProfilePages));
            },
            cancellationToken);

    public Task<SettingsCatalogDto?> GetSettingsCatalogAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : services.GetRequiredService<StructuredSettingsService>().GetCatalog(profile);
            },
            cancellationToken);

    public Task<SettingsValueSetDto?> GetSettingsPageAsync(
        string profileId,
        string pageId,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : services.GetRequiredService<StructuredSettingsService>().GetPage(profile, pageId);
            },
            cancellationToken);

    public Task<SettingsValidationResultDto?> ValidateSettingsPageAsync(
        string profileId,
        string pageId,
        SettingsValueSetDto payload,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : services.GetRequiredService<StructuredSettingsService>().Validate(profile, pageId, payload.Values);
            },
            cancellationToken);

    public Task<SettingsSaveResultDto?> SaveSettingsPageAsync(
        string profileId,
        string pageId,
        SettingsValueSetDto payload,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profileStore = services.GetRequiredService<ProfileStore>();
                var profile = await profileStore.GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var structuredSettingsService = services.GetRequiredService<StructuredSettingsService>();
                var result = await structuredSettingsService.SaveAsync(profile, pageId, payload.Values, cancellationToken);
                if (result.Validation.IsValid && !result.Validation.RequiresAdvancedFilesFallback)
                {
                    await services.GetRequiredService<AuditStore>().WriteAsync(
                        "settings.page.updated",
                        profileId,
                        "desktop",
                        $"Updated structured settings page {pageId}.",
                        cancellationToken: cancellationToken);
                }

                return result;
            },
            cancellationToken);

    public Task<SettingsDraftDto?> GetSettingsDraftAsync(string profileId, string pageId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var structuredSettingsService = services.GetRequiredService<StructuredSettingsService>();
                var catalog = structuredSettingsService.GetCatalog(profile);
                return await services.GetRequiredService<SettingsDraftStore>()
                    .GetAsync(profileId, profile.Branch, catalog.CatalogId, catalog.CatalogVersion, pageId, cancellationToken);
            },
            cancellationToken);

    public Task<SettingsDraftDto?> SaveSettingsDraftAsync(
        string profileId,
        string pageId,
        SettingsDraftDto payload,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var structuredSettingsService = services.GetRequiredService<StructuredSettingsService>();
                var catalog = structuredSettingsService.GetCatalog(profile);
                var normalized = payload with
                {
                    ProfileId = profileId,
                    Branch = profile.Branch,
                    CatalogId = catalog.CatalogId,
                    CatalogVersion = catalog.CatalogVersion,
                    PageId = pageId,
                    UpdatedAtUtc = payload.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : payload.UpdatedAtUtc,
                };

                return await services.GetRequiredService<SettingsDraftStore>().UpsertAsync(normalized, cancellationToken);
            },
            cancellationToken);

    public Task DeleteSettingsDraftAsync(string profileId, string pageId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
                var structuredSettingsService = services.GetRequiredService<StructuredSettingsService>();
                var catalog = structuredSettingsService.GetCatalog(profile);
                await services.GetRequiredService<SettingsDraftStore>()
                    .DeleteAsync(profileId, profile.Branch, catalog.CatalogId, catalog.CatalogVersion, pageId, cancellationToken);
                return 0;
            },
            cancellationToken);

    public Task<HostSettings?> GetHostSettingsAsync(CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services => await services.GetRequiredService<HostSettingsStore>().GetAsync(cancellationToken),
            cancellationToken);

    public Task<HostSettings?> UpdateHostSettingsAsync(HostSettings settings, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services => await services.GetRequiredService<HostSettingsStore>().UpdateAsync(settings, null, cancellationToken),
            cancellationToken);
}
