using Microsoft.Extensions.Hosting;

namespace PZServerLauncher.Host.Services;

public sealed class ProfileAutoStartService(
    IServiceScopeFactory scopeFactory,
    ServerProcessSupervisor processSupervisor,
    ILogger<ProfileAutoStartService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

            await using var scope = scopeFactory.CreateAsyncScope();
            var profileStore = scope.ServiceProvider.GetRequiredService<ProfileStore>();
            var profiles = await profileStore.ListAsync(stoppingToken);

            foreach (var profile in profiles.Where(x => x.StartWithHost))
            {
                if (processSupervisor.IsRunning(profile.ProfileId))
                {
                    continue;
                }

                try
                {
                    await processSupervisor.StartAsync(profile, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Failed to auto-start profile {ProfileId}.", profile.ProfileId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
