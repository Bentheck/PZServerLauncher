using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class ScheduledBackupService(
    IServiceScopeFactory scopeFactory,
    BackgroundJobDispatcher dispatcher,
    ILogger<ScheduledBackupService> logger)
    : BackgroundService
{
    private readonly ConcurrentDictionary<string, byte> _queuedSlots = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            await ScanAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ScanAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var profileStore = scope.ServiceProvider.GetRequiredService<ProfileStore>();
        var backupService = scope.ServiceProvider.GetRequiredService<ServerBackupService>();
        var profiles = await profileStore.ListAsync(cancellationToken);
        var nowUtc = DateTimeOffset.UtcNow;

        foreach (var profile in profiles.Where(candidate => candidate.BackupPolicy.ScheduledBackupsEnabled))
        {
            try
            {
                var lastScheduledBackupUtc = backupService.GetLatestScheduledBackupTimestampUtc(profile.ProfileId);
                if (!ScheduledBackupPlanner.TryGetDueScheduledRunUtc(
                        profile.BackupPolicy,
                        nowUtc,
                        lastScheduledBackupUtc,
                        out var dueRunUtc))
                {
                    continue;
                }

                var queueKey = $"{profile.ProfileId}|{dueRunUtc:O}";
                if (!_queuedSlots.TryAdd(queueKey, 0))
                {
                    continue;
                }

                try
                {
                    await dispatcher.QueueAsync(
                        OperationJobKind.Backup,
                        profile.ProfileId,
                        $"Scheduled backup {profile.ProfileId}",
                        async (services, runningJob, token) =>
                        {
                            try
                            {
                                var scopedBackupService = services.GetRequiredService<ServerBackupService>();
                                var jobStore = services.GetRequiredService<JobStore>();
                                var zipPath = await scopedBackupService.CreateBackupAsync(profile.ProfileId, BackupTrigger.Scheduled, token);
                                await jobStore.UpdateAsync(runningJob with
                                {
                                    Status = OperationJobStatus.Succeeded,
                                    ProgressPercent = 100,
                                    Detail = $"Created scheduled backup {Path.GetFileName(zipPath)}.",
                                    CompletedAtUtc = DateTimeOffset.UtcNow,
                                }, token);
                            }
                            finally
                            {
                                _queuedSlots.TryRemove(queueKey, out _);
                            }
                        },
                        cancellationToken);
                }
                catch
                {
                    _queuedSlots.TryRemove(queueKey, out _);
                    throw;
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to evaluate scheduled backup for profile {ProfileId}.", profile.ProfileId);
            }
        }
    }
}
