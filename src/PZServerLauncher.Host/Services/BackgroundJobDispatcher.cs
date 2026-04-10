using Microsoft.AspNetCore.SignalR;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Hubs;

namespace PZServerLauncher.Host.Services;

public sealed class BackgroundJobDispatcher(IServiceScopeFactory scopeFactory, IHubContext<RuntimeHub> hubContext)
{
    public async Task<OperationJob> QueueAsync(
        OperationJobKind kind,
        string? profileId,
        string summary,
        Func<IServiceProvider, OperationJob, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        await using var initialScope = scopeFactory.CreateAsyncScope();
        var jobStore = initialScope.ServiceProvider.GetRequiredService<JobStore>();
        var job = await jobStore.CreateAsync(kind, profileId, summary, cancellationToken);

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
                await hubContext.Clients.All.SendAsync("jobChanged", running);

                await work(scope.ServiceProvider, running, CancellationToken.None);

                var completed = await scopedJobStore.GetAsync(job.JobId, CancellationToken.None);
                if (completed is not null)
                {
                    await hubContext.Clients.All.SendAsync("jobChanged", completed);
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
                await hubContext.Clients.All.SendAsync("jobChanged", failed);
            }
        }, CancellationToken.None);

        return job;
    }
}
