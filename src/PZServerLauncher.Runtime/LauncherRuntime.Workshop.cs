using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    public Task<WorkshopScanResultDto?> ScanWorkshopAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var structuredSettingsService = services.GetRequiredService<StructuredSettingsService>();
                return services.GetRequiredService<WorkshopPresetScannerService>()
                    .Scan(profile.InstallDirectory, structuredSettingsService.GetWorkshopPreset(profile));
            },
            cancellationToken);

    public Task<WorkshopCatalogSearchResultDto?> SearchWorkshopCatalogAsync(
        string profileId,
        WorkshopCatalogSearchRequestDto request,
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
                var preset = request.CurrentPreset ?? structuredSettingsService.GetWorkshopPreset(profile);
                return await services.GetRequiredService<WorkshopCatalogService>()
                    .SearchAsync(profile, preset, request, cancellationToken);
            },
            cancellationToken);

    public Task<WorkshopCatalogPreviewDto?> GetWorkshopCatalogPreviewAsync(
        string profileId,
        string workshopId,
        WorkshopCatalogPreviewRequestDto request,
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
                var preset = request.CurrentPreset ?? structuredSettingsService.GetWorkshopPreset(profile);
                return await services.GetRequiredService<WorkshopCatalogService>()
                    .GetPreviewAsync(profile, preset, workshopId, request.SearchMode, cancellationToken);
            },
            cancellationToken);

    public Task<SteamWorkshopBrowserSettingsDto?> GetWorkshopBrowserSettingsAsync(CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services => await services.GetRequiredService<WorkshopBrowserSettingsStore>().GetAsync(cancellationToken),
            cancellationToken);

    public Task<SteamWorkshopBrowserSettingsDto?> SetSteamWebApiKeyAsync(string apiKey, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var updated = await services.GetRequiredService<WorkshopBrowserSettingsStore>().SetSteamWebApiKeyAsync(apiKey, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "settings.workshop-browser.api-key.updated",
                    "host",
                    "desktop",
                    updated.HasSteamWebApiKeyConfigured
                        ? "Stored or replaced the Steam Web API key for Workshop search."
                        : "Cleared the Steam Web API key for Workshop search.",
                    cancellationToken: cancellationToken);
                return updated;
            },
            cancellationToken);

    public Task<SteamWorkshopBrowserSettingsDto?> RemoveSteamWebApiKeyAsync(CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var updated = await services.GetRequiredService<WorkshopBrowserSettingsStore>().RemoveSteamWebApiKeyAsync(cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "settings.workshop-browser.api-key.deleted",
                    "host",
                    "desktop",
                    "Removed the Steam Web API key for Workshop search.",
                    cancellationToken: cancellationToken);
                return updated;
            },
            cancellationToken);

    public async Task<byte[]?> DownloadWorkshopImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteUri) &&
            absoluteUri.IsFile &&
            File.Exists(absoluteUri.LocalPath))
        {
            return await File.ReadAllBytesAsync(absoluteUri.LocalPath, cancellationToken);
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(25),
            };
            using var request = new HttpRequestMessage(HttpMethod.Get, absoluteUri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PZServerLauncher", "1.0"));
            request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        if (File.Exists(imageUrl))
        {
            return await File.ReadAllBytesAsync(imageUrl, cancellationToken);
        }

        if (!TryParseWorkshopImagePath(imageUrl, out var profileId, out var workshopId, out var source))
        {
            return null;
        }

        return await ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var image = await services.GetRequiredService<WorkshopCatalogService>()
                    .GetImageAsync(profile, workshopId, source, cancellationToken);
                return image?.Content;
            },
            cancellationToken);
    }

    public Task<WorkshopPreset?> GetWorkshopPresetAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : services.GetRequiredService<StructuredSettingsService>().GetWorkshopPreset(profile);
            },
            cancellationToken);

    public Task<WorkshopPreset?> UpdateWorkshopPresetAsync(string profileId, WorkshopPreset preset, CancellationToken cancellationToken = default) =>
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
                var normalizedPreset = await structuredSettingsService.SaveWorkshopPresetAsync(profile, preset, cancellationToken);
                var updated = await profileStore.GetAsync(profileId, cancellationToken) ?? profile with { WorkshopPreset = normalizedPreset };
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.workshop-preset.updated",
                    profileId,
                    "desktop",
                    $"Updated workshop preset for {updated.DisplayName}.",
                    cancellationToken: cancellationToken);
                return normalizedPreset;
            },
            cancellationToken);

    public Task<List<NamedWorkshopPresetDto>?> GetNamedWorkshopPresetsAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : (await services.GetRequiredService<NamedWorkshopPresetStore>().ListAsync(profileId, cancellationToken)).ToList();
            },
            cancellationToken);

    public Task<NamedWorkshopPresetDto?> SaveNamedWorkshopPresetAsync(
        string profileId,
        string name,
        WorkshopPreset preset,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var normalizedPreset = services.GetRequiredService<WorkshopPresetScannerService>()
                    .Scan(profile.InstallDirectory, preset)
                    .Preset;
                var savedPreset = await services.GetRequiredService<NamedWorkshopPresetStore>()
                    .UpsertAsync(profileId, profile.Branch, name, normalizedPreset, cancellationToken);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.workshop-preset-library.updated",
                    profileId,
                    "desktop",
                    $"Saved named workshop preset '{savedPreset.Name}' for {profile.DisplayName}.",
                    cancellationToken: cancellationToken);
                return savedPreset;
            },
            cancellationToken);

    public Task DeleteNamedWorkshopPresetAsync(string profileId, Guid presetId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
                var deleted = await services.GetRequiredService<NamedWorkshopPresetStore>()
                    .DeleteAsync(profileId, presetId, cancellationToken);
                if (!deleted)
                {
                    throw new KeyNotFoundException($"Named preset '{presetId}' was not found for profile '{profileId}'.");
                }

                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.workshop-preset-library.deleted",
                    profileId,
                    "desktop",
                    $"Deleted a named workshop preset for {profile.DisplayName}.",
                    cancellationToken: cancellationToken);
                return 0;
            },
            cancellationToken);

    public Task<RawConfigFileDto?> GetRawConfigAsync(
        string profileId,
        ConfigFileKind kind,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : services.GetRequiredService<ConfigFileService>().ReadRawFile(profile, kind);
            },
            cancellationToken);

    public Task<RawConfigFileDto?> SaveRawConfigAsync(
        string profileId,
        ConfigFileKind kind,
        RawConfigFileDto payload,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var updated = services.GetRequiredService<ConfigFileService>().WriteRawFile(profile, kind, payload.Sha256, payload.Content);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "config.file.updated",
                    profileId,
                    "desktop",
                    $"Updated raw config {kind}.",
                    cancellationToken: cancellationToken);
                return updated;
            },
            cancellationToken);
}
