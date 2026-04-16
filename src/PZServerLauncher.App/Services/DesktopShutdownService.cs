using PZServerLauncher.Runtime;

namespace PZServerLauncher.App.Services;

public sealed class DesktopShutdownService(
    ILauncherRuntime launcherRuntime,
    DesktopLogService logService)
{
    public async Task StopAppAndServersAsync(CancellationToken cancellationToken = default)
    {
        logService.Info("Desktop shutdown requested. Stopping managed servers and the embedded runtime.");

        try
        {
            await launcherRuntime.StopRuntimeAsync(stopRunningServers: true, cancellationToken);
        }
        catch (Exception ex)
        {
            logService.Error("Failed to stop the integrated runtime during desktop shutdown.", ex);
        }
    }
}
