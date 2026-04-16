using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class BackgroundJobDispatcher(IServiceScopeFactory scopeFactory, IRuntimeEventPublisher runtimeEventPublisher)
{
    private readonly SemaphoreSlim _queueGate = new(1, 1);

    public async Task<OperationJob> QueueAsync(
        OperationJobKind kind,
        string? profileId,
        string summary,
        Func<IServiceProvider, OperationJob, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        OperationJob job;
        await _queueGate.WaitAsync(cancellationToken);
        try
        {
            await using var initialScope = scopeFactory.CreateAsyncScope();
            var jobStore = initialScope.ServiceProvider.GetRequiredService<JobStore>();

            if (RequiresExclusiveLifecycleQueue(kind) && !string.IsNullOrWhiteSpace(profileId))
            {
                var activeJob = await jobStore.GetActiveProfileLifecycleJobAsync(profileId, cancellationToken);
                if (activeJob is not null)
                {
                    throw new InvalidOperationException(
                        $"An install or update is already running for '{profileId}'. Wait for job {activeJob.JobId:N} to finish before queueing another maintenance action.");
                }
            }

            job = await jobStore.CreateAsync(kind, profileId, summary, cancellationToken);
        }
        finally
        {
            _queueGate.Release();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var scopedJobStore = scope.ServiceProvider.GetRequiredService<JobStore>();
                var running = await scopedJobStore.UpdateAsync(job with
                {
                    Status = OperationJobStatus.Running,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    ProgressPercent = 5,
                }, CancellationToken.None);
                await runtimeEventPublisher.PublishJobChangedAsync(running);

                await work(scope.ServiceProvider, running, CancellationToken.None);

                var completed = await scopedJobStore.GetAsync(job.JobId, CancellationToken.None);
                if (completed is not null)
                {
                    await runtimeEventPublisher.PublishJobChangedAsync(completed);
                }
            }
            catch (Exception ex)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var scopedJobStore = scope.ServiceProvider.GetRequiredService<JobStore>();
                var failed = await scopedJobStore.UpdateAsync(job with
                {
                    Status = OperationJobStatus.Failed,
                    ProgressPercent = 100,
                    Detail = ex.Message,
                    StartedAtUtc = job.StartedAtUtc ?? DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                }, CancellationToken.None);
                await runtimeEventPublisher.PublishJobChangedAsync(failed);
            }
        }, CancellationToken.None);

        return job;
    }

    private static bool RequiresExclusiveLifecycleQueue(OperationJobKind kind) =>
        kind is OperationJobKind.Install or OperationJobKind.Update;
}
