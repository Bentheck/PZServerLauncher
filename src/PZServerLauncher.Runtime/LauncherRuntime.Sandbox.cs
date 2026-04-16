using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    public Task<List<SandboxPresetDto>?> GetSandboxPresetsAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : services.GetRequiredService<SandboxPresetLibraryService>().List(profile).ToList();
            },
            cancellationToken);

    public Task<SandboxPresetDto?> SaveSandboxPresetAsync(
        string profileId,
        string name,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var preset = services.GetRequiredService<SandboxPresetLibraryService>().Save(profile, name, values);
                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.sandbox-preset-library.updated",
                    profileId,
                    "desktop",
                    $"Saved sandbox preset '{preset.Label}' for {profile.DisplayName}.",
                    cancellationToken: cancellationToken);
                return preset;
            },
            cancellationToken);

    public Task DeleteSandboxPresetAsync(string profileId, string presetId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
                var deleted = services.GetRequiredService<SandboxPresetLibraryService>().Delete(profile, presetId);
                if (!deleted)
                {
                    throw new KeyNotFoundException($"Sandbox preset '{presetId}' was not found for profile '{profileId}'.");
                }

                await services.GetRequiredService<AuditStore>().WriteAsync(
                    "profile.sandbox-preset-library.deleted",
                    profileId,
                    "desktop",
                    $"Deleted sandbox preset '{presetId}' for {profile.DisplayName}.",
                    cancellationToken: cancellationToken);
                return 0;
            },
            cancellationToken);
}
