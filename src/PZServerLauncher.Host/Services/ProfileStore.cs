using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Data;

namespace PZServerLauncher.Host.Services;

public sealed class ProfileStore(ApplicationDbContext dbContext)
{
    public async Task<IReadOnlyList<ServerProfile>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.ServerProfiles
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => x.ToModel())
            .ToListAsync(cancellationToken);

    public async Task<ServerProfile?> GetAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ServerProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.ProfileId == profileId, cancellationToken);
        return entity?.ToModel();
    }

    public async Task<ServerProfile> UpsertAsync(ServerProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ServerProfiles.SingleOrDefaultAsync(x => x.ProfileId == profile.ProfileId, cancellationToken);
        if (existing is null)
        {
            existing = (profile with
            {
                CreatedAtUtc = profile.CreatedAtUtc == default ? DateTimeOffset.UtcNow : profile.CreatedAtUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            }).ToEntity();
            dbContext.ServerProfiles.Add(existing);
        }
        else
        {
            existing.ApplyModel(profile with { UpdatedAtUtc = DateTimeOffset.UtcNow });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing.ToModel();
    }

    public async Task<bool> DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ServerProfiles.SingleOrDefaultAsync(x => x.ProfileId == profileId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.ServerProfiles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
