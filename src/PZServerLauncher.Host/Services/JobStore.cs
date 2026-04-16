using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;

namespace PZServerLauncher.Host.Services;

public sealed class JobStore(ApplicationDbContext dbContext)
{
    public async Task<OperationJob> CreateAsync(
        OperationJobKind kind,
        string? profileId,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var job = new OperationJob(
            Guid.NewGuid(),
            kind,
            OperationJobStatus.Queued,
            profileId,
            summary,
            null,
            0,
            DateTimeOffset.UtcNow,
            null,
            null);

        dbContext.OperationJobs.Add(job.ToEntity());
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<OperationJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OperationJobs.AsNoTracking().SingleOrDefaultAsync(x => x.JobId == jobId, cancellationToken);
        return entity?.ToModel();
    }

    public async Task<OperationJob?> GetActiveProfileLifecycleJobAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var statuses = new[] { (int)OperationJobStatus.Queued, (int)OperationJobStatus.Running };
        var kinds = new[] { (int)OperationJobKind.Install, (int)OperationJobKind.Update };

        var entities = await dbContext.OperationJobs
            .AsNoTracking()
            .Where(job =>
                job.ProfileId == profileId &&
                statuses.Contains(job.Status) &&
                kinds.Contains(job.Kind))
            .ToListAsync(cancellationToken);

        var entity = entities
            .OrderByDescending(job => job.CreatedAtUtc)
            .FirstOrDefault();

        return entity?.ToModel();
    }

    public async Task<OperationJob?> GetActiveProfileJobAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        var statuses = new[] { (int)OperationJobStatus.Queued, (int)OperationJobStatus.Running };

        var entities = await dbContext.OperationJobs
            .AsNoTracking()
            .Where(job =>
                job.ProfileId == profileId &&
                statuses.Contains(job.Status))
            .ToListAsync(cancellationToken);

        var entity = entities
            .OrderByDescending(job => job.CreatedAtUtc)
            .FirstOrDefault();

        return entity?.ToModel();
    }

    public async Task<IReadOnlyList<OperationJob>> ListRecentAsync(int take = 20, CancellationToken cancellationToken = default) =>
        (await dbContext.OperationJobs
            .AsNoTracking()
            .Select(x => x.ToModel())
            .ToListAsync(cancellationToken))
        .OrderByDescending(x => x.CreatedAtUtc)
        .Take(take)
        .ToArray();

    public async Task<OperationJob> UpdateAsync(OperationJob job, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OperationJobs.SingleAsync(x => x.JobId == job.JobId, cancellationToken);
        entity.Kind = (int)job.Kind;
        entity.Status = (int)job.Status;
        entity.ProfileId = job.ProfileId;
        entity.Summary = job.Summary;
        entity.Detail = job.Detail;
        entity.ProgressPercent = job.ProgressPercent;
        entity.StartedAtUtc = job.StartedAtUtc;
        entity.CompletedAtUtc = job.CompletedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToModel();
    }
}
