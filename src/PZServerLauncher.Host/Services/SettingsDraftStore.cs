using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;

namespace PZServerLauncher.Host.Services;

public sealed class SettingsDraftStore(ApplicationDbContext dbContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<SettingsDraftDto?> GetAsync(
        string profileId,
        ProjectZomboidBranch branch,
        string catalogId,
        int catalogVersion,
        string pageId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<SettingsDraftEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.ProfileId == profileId &&
                    x.Branch == (int)branch &&
                    x.CatalogId == catalogId &&
                    x.CatalogVersion == catalogVersion &&
                    x.PageId == pageId,
                cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<SettingsDraftDto> UpsertAsync(SettingsDraftDto draft, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<SettingsDraftEntity>()
            .SingleOrDefaultAsync(
                x => x.ProfileId == draft.ProfileId &&
                    x.Branch == (int)draft.Branch &&
                    x.CatalogId == draft.CatalogId &&
                    x.CatalogVersion == draft.CatalogVersion &&
                    x.PageId == draft.PageId,
                cancellationToken);

        if (entity is null)
        {
            entity = ToEntity(draft);
            dbContext.Add(entity);
        }
        else
        {
            entity.ValuesJson = JsonSerializer.Serialize(draft.Values, SerializerOptions);
            entity.SourceSha256 = draft.SourceSha256;
            entity.IsDirty = draft.IsDirty;
            entity.UpdatedAtUtc = draft.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : draft.UpdatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(
        string profileId,
        ProjectZomboidBranch branch,
        string catalogId,
        int catalogVersion,
        string pageId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<SettingsDraftEntity>()
            .SingleOrDefaultAsync(
                x => x.ProfileId == profileId &&
                    x.Branch == (int)branch &&
                    x.CatalogId == catalogId &&
                    x.CatalogVersion == catalogVersion &&
                    x.PageId == pageId,
                cancellationToken);

        if (entity is null)
        {
            return false;
        }

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SettingsDraftDto ToDto(SettingsDraftEntity entity) =>
        new(
            entity.ProfileId,
            ProjectZomboidBranchSupport.FromPersistedValue(entity.Branch),
            entity.CatalogId,
            entity.CatalogVersion,
            entity.PageId,
            JsonSerializer.Deserialize<Dictionary<string, string?>>(entity.ValuesJson, SerializerOptions) ?? [],
            entity.SourceSha256,
            entity.IsDirty,
            entity.UpdatedAtUtc);

    private static SettingsDraftEntity ToEntity(SettingsDraftDto draft) =>
        new()
        {
            ProfileId = draft.ProfileId,
            Branch = (int)draft.Branch,
            CatalogId = draft.CatalogId,
            CatalogVersion = draft.CatalogVersion,
            PageId = draft.PageId,
            ValuesJson = JsonSerializer.Serialize(draft.Values, SerializerOptions),
            SourceSha256 = draft.SourceSha256,
            IsDirty = draft.IsDirty,
            UpdatedAtUtc = draft.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : draft.UpdatedAtUtc,
        };
}
