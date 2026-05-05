using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Runtime;

public sealed partial class LauncherRuntime
{
    public Task<LauncherUpdateStatusDto> GetLauncherUpdateStatusAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) =>
        ExecuteScopedAsync(
            async services => await services.GetRequiredService<LauncherReleaseService>()
                .GetStatusAsync(forceRefresh, cancellationToken),
            cancellationToken);
}
