using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    public Task<ModsMapsDraftDto?> GetModsMapsDraftAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                return profile is null
                    ? null
                    : await services.GetRequiredService<ModsMapsDraftStore>().GetAsync(profileId, cancellationToken);
            },
            cancellationToken);

    public Task<ModsMapsDraftDto?> SaveModsMapsDraftAsync(string profileId, ModsMapsDraftDto payload, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken);
                if (profile is null)
                {
                    return null;
                }

                var normalized = payload with
                {
                    ProfileId = profileId,
                    Branch = profile.Branch,
                    UpdatedAtUtc = payload.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : payload.UpdatedAtUtc,
                };

                return await services.GetRequiredService<ModsMapsDraftStore>().UpsertAsync(normalized, cancellationToken);
            },
            cancellationToken);

    public Task DeleteModsMapsDraftAsync(string profileId, CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services =>
            {
                var profile = await services.GetRequiredService<ProfileStore>().GetAsync(profileId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
                await services.GetRequiredService<ModsMapsDraftStore>().DeleteAsync(profile.ProfileId, cancellationToken);
                return 0;
            },
            cancellationToken);
}
