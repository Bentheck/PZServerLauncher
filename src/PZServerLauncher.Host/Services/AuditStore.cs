using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;

namespace PZServerLauncher.Host.Services;

public sealed class AuditStore(ApplicationDbContext dbContext)
{
    public async Task WriteAsync(
        string action,
        string subject,
        string actorType,
        string detail,
        string? actorId = null,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditEntries.Add(new AuditEntryEntity
        {
            EntryId = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Action = action,
            Subject = subject,
            ActorType = actorType,
            ActorId = actorId,
            Detail = detail,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> ListAsync(CancellationToken cancellationToken = default) =>
        (await dbContext.AuditEntries
            .AsNoTracking()
            .Select(x => x.ToModel())
            .ToListAsync(cancellationToken))
        .OrderByDescending(x => x.OccurredAtUtc)
        .Take(100)
        .ToArray();
}
