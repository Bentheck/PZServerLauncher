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

    public async Task<string?> GetPortConflictMessageAsync(ServerProfile profile, CancellationToken cancellationToken = default)
    {
        var requestedPorts = new[]
        {
            (Port: profile.DefaultPort, Label: "game"),
            (Port: profile.UdpPort, Label: "UDP"),
            (Port: profile.RconPort, Label: "RCON"),
        };

        var otherProfiles = await dbContext.ServerProfiles
            .AsNoTracking()
            .Where(candidate => candidate.ProfileId != profile.ProfileId)
            .OrderBy(candidate => candidate.DisplayName)
            .Select(candidate => new
            {
                candidate.ProfileId,
                candidate.DisplayName,
                candidate.DefaultPort,
                candidate.UdpPort,
                candidate.RconPort,
            })
            .ToListAsync(cancellationToken);

        foreach (var existingProfile in otherProfiles)
        {
            var reservedPorts = new Dictionary<int, string>
            {
                [existingProfile.DefaultPort] = "game",
                [existingProfile.UdpPort] = "UDP",
                [existingProfile.RconPort] = "RCON",
            };

            foreach (var requestedPort in requestedPorts)
            {
                if (reservedPorts.ContainsKey(requestedPort.Port))
                {
                    return $"Port {requestedPort.Port} for the new {requestedPort.Label} endpoint is already used by {existingProfile.DisplayName} ({existingProfile.DefaultPort} / {existingProfile.UdpPort} / {existingProfile.RconPort}). Choose a different base port.";
                }
            }
        }

        return null;
    }

    public async Task<ServerProfile> UpsertAsync(ServerProfile profile, CancellationToken cancellationToken = default)
    {
        EnsureManagedRootDirectories(profile);

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

    private static void EnsureManagedRootDirectories(ServerProfile profile)
    {
        if (ServerProfileFactory.IsManagedInstallDirectory(profile.InstallDirectory))
        {
            var installRoot = Directory.GetParent(Path.GetFullPath(profile.InstallDirectory));
            if (installRoot is not null)
            {
                Directory.CreateDirectory(installRoot.FullName);
            }
        }

        if (ServerProfileFactory.IsManagedCacheDirectory(profile.CacheDirectory))
        {
            var cacheRoot = Directory.GetParent(Path.GetFullPath(profile.CacheDirectory));
            if (cacheRoot is not null)
            {
                Directory.CreateDirectory(cacheRoot.FullName);
            }
        }
    }
}
