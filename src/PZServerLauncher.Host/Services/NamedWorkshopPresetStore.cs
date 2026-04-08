using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;

namespace PZServerLauncher.Host.Services;

public sealed class NamedWorkshopPresetStore(ApplicationDbContext dbContext)
{
    public async Task<IReadOnlyList<NamedWorkshopPresetDto>> ListAsync(string profileId, CancellationToken cancellationToken = default) =>
        await dbContext.NamedWorkshopPresets
            .AsNoTracking()
            .Where(entity => entity.ProfileId == profileId)
            .OrderByDescending(entity => entity.UpdatedAtUtc)
            .ThenBy(entity => entity.Name)
            .Select(entity => entity.ToDto())
            .ToListAsync(cancellationToken);

    public async Task<NamedWorkshopPresetDto> UpsertAsync(
        string profileId,
        ProjectZomboidBranch branch,
        string name,
        WorkshopPreset preset,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new InvalidOperationException("Preset name is required.");
        }

        var normalizedName = trimmedName.ToUpperInvariant();
        var entity = await dbContext.NamedWorkshopPresets
            .SingleOrDefaultAsync(
                candidate => candidate.ProfileId == profileId && candidate.NormalizedName == normalizedName,
                cancellationToken);

        if (entity is null)
        {
            entity = new NamedWorkshopPresetEntity
            {
                PresetId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            dbContext.NamedWorkshopPresets.Add(entity);
        }

        entity.ApplyModel(profileId, branch, trimmedName, preset);

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(string profileId, Guid presetId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.NamedWorkshopPresets
            .SingleOrDefaultAsync(candidate => candidate.ProfileId == profileId && candidate.PresetId == presetId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.NamedWorkshopPresets.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
