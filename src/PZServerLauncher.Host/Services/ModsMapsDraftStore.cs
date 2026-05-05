using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Data.Entities;

namespace PZServerLauncher.Host.Services;

public sealed class ModsMapsDraftStore(ApplicationDbContext dbContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ModsMapsDraftDto?> GetAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.ModsMapsDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.ProfileId == profileId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        var modRows = await dbContext.ModsMapsDraftModRows
            .AsNoTracking()
            .Where(entity => entity.ProfileId == profileId)
            .OrderBy(entity => entity.SortOrder)
            .ThenBy(entity => entity.RowId)
            .ToListAsync(cancellationToken);
        var mapRows = await dbContext.ModsMapsDraftMapRows
            .AsNoTracking()
            .Where(entity => entity.ProfileId == profileId)
            .OrderBy(entity => entity.SortOrder)
            .ThenBy(entity => entity.RowId)
            .ToListAsync(cancellationToken);

        return new ModsMapsDraftDto(
            draft.ProfileId,
            ProjectZomboidBranchSupport.FromPersistedValue(draft.Branch),
            DeserializeList(draft.WorkshopItemIdsJson),
            modRows.Select(ToDto).ToArray(),
            mapRows.Select(ToDto).ToArray(),
            ParseEditorMode(draft.EditorMode),
            draft.IsDirty,
            draft.UpdatedAtUtc);
    }

    public async Task<ModsMapsDraftDto> UpsertAsync(ModsMapsDraftDto draft, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeDraft(draft);
        var entity = await dbContext.ModsMapsDrafts
            .SingleOrDefaultAsync(candidate => candidate.ProfileId == normalized.ProfileId, cancellationToken);
        if (entity is null)
        {
            entity = new ModsMapsDraftEntity
            {
                ProfileId = normalized.ProfileId,
            };
            dbContext.ModsMapsDrafts.Add(entity);
        }

        entity.Branch = (int)normalized.Branch;
        entity.WorkshopItemIdsJson = JsonSerializer.Serialize(normalized.WorkshopItemIds, SerializerOptions);
        entity.EditorMode = normalized.EditorMode.ToString();
        entity.IsDirty = normalized.IsDirty;
        entity.UpdatedAtUtc = normalized.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : normalized.UpdatedAtUtc;

        var existingModRows = await dbContext.ModsMapsDraftModRows
            .Where(candidate => candidate.ProfileId == normalized.ProfileId)
            .ToListAsync(cancellationToken);
        var existingMapRows = await dbContext.ModsMapsDraftMapRows
            .Where(candidate => candidate.ProfileId == normalized.ProfileId)
            .ToListAsync(cancellationToken);

        dbContext.ModsMapsDraftModRows.RemoveRange(existingModRows);
        dbContext.ModsMapsDraftMapRows.RemoveRange(existingMapRows);

        dbContext.ModsMapsDraftModRows.AddRange(normalized.ModRows.Select(row => new ModsMapsDraftModRowEntity
        {
            ProfileId = normalized.ProfileId,
            RowId = row.RowId,
            ModName = row.ModName,
            ModId = row.ModId,
            WorkshopId = row.WorkshopId,
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            DependencyModIdsJson = JsonSerializer.Serialize(row.DependencyModIds, SerializerOptions),
            MapFoldersJson = JsonSerializer.Serialize(row.MapFolders, SerializerOptions),
        }));
        dbContext.ModsMapsDraftMapRows.AddRange(normalized.MapRows.Select(row => new ModsMapsDraftMapRowEntity
        {
            ProfileId = normalized.ProfileId,
            RowId = row.RowId,
            Title = row.Title,
            MapFolder = row.MapFolder,
            WorkshopId = row.WorkshopId,
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return normalized with { UpdatedAtUtc = entity.UpdatedAtUtc };
    }

    public async Task<bool> DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var draft = await dbContext.ModsMapsDrafts
            .SingleOrDefaultAsync(entity => entity.ProfileId == profileId, cancellationToken);
        if (draft is null)
        {
            return false;
        }

        var modRows = await dbContext.ModsMapsDraftModRows
            .Where(entity => entity.ProfileId == profileId)
            .ToListAsync(cancellationToken);
        var mapRows = await dbContext.ModsMapsDraftMapRows
            .Where(entity => entity.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        dbContext.ModsMapsDraftModRows.RemoveRange(modRows);
        dbContext.ModsMapsDraftMapRows.RemoveRange(mapRows);
        dbContext.ModsMapsDrafts.Remove(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ModsMapsDraftDto NormalizeDraft(ModsMapsDraftDto draft)
    {
        var workshopItemIds = DistinctNonEmpty(draft.WorkshopItemIds);
        var modRows = NormalizeModRows(draft.ModRows);
        var mapRows = NormalizeMapRows(draft.MapRows);

        return draft with
        {
            WorkshopItemIds = workshopItemIds,
            ModRows = modRows,
            MapRows = mapRows,
            UpdatedAtUtc = draft.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : draft.UpdatedAtUtc,
        };
    }

    private static IReadOnlyList<ModsMapsModRowDto> NormalizeModRows(IReadOnlyList<ModsMapsModRowDto> rows)
    {
        var normalized = new List<ModsMapsModRowDto>(rows.Count);
        var seenModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRowIds = new HashSet<int>();
        var nextRowId = 1;

        foreach (var row in rows
                     .Where(candidate => !string.IsNullOrWhiteSpace(candidate.ModId))
                     .OrderBy(candidate => candidate.SortOrder)
                     .ThenBy(candidate => candidate.RowId))
        {
            var modId = row.ModId.Trim();
            if (!seenModIds.Add(modId))
            {
                continue;
            }

            var rowId = row.RowId > 0 && seenRowIds.Add(row.RowId)
                ? row.RowId
                : AllocateRowId(seenRowIds, ref nextRowId);
            nextRowId = Math.Max(nextRowId, rowId + 1);

            normalized.Add(new ModsMapsModRowDto(
                rowId,
                string.IsNullOrWhiteSpace(row.ModName) ? modId : row.ModName.Trim(),
                modId,
                row.WorkshopId?.Trim() ?? string.Empty,
                row.IsActive,
                normalized.Count,
                DistinctNonEmpty(row.DependencyModIds),
                DistinctNonEmpty(row.MapFolders)));
        }

        return normalized;
    }

    private static IReadOnlyList<ModsMapsMapRowDto> NormalizeMapRows(IReadOnlyList<ModsMapsMapRowDto> rows)
    {
        var normalized = new List<ModsMapsMapRowDto>(rows.Count);
        var seenMapFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRowIds = new HashSet<int>();
        var nextRowId = 1;

        foreach (var row in rows
                     .Where(candidate => !string.IsNullOrWhiteSpace(candidate.MapFolder))
                     .OrderBy(candidate => candidate.SortOrder)
                     .ThenBy(candidate => candidate.RowId))
        {
            var mapFolder = row.MapFolder.Trim();
            if (!seenMapFolders.Add(mapFolder))
            {
                continue;
            }

            var rowId = row.RowId > 0 && seenRowIds.Add(row.RowId)
                ? row.RowId
                : AllocateRowId(seenRowIds, ref nextRowId);
            nextRowId = Math.Max(nextRowId, rowId + 1);

            normalized.Add(new ModsMapsMapRowDto(
                rowId,
                string.IsNullOrWhiteSpace(row.Title) ? mapFolder : row.Title.Trim(),
                mapFolder,
                row.WorkshopId?.Trim() ?? string.Empty,
                row.IsActive,
                normalized.Count));
        }

        return normalized;
    }

    private static int AllocateRowId(ISet<int> seenRowIds, ref int nextRowId)
    {
        while (!seenRowIds.Add(nextRowId))
        {
            nextRowId++;
        }

        return nextRowId++;
    }

    private static ModsMapsModRowDto ToDto(ModsMapsDraftModRowEntity entity) =>
        new(
            entity.RowId,
            entity.ModName,
            entity.ModId,
            entity.WorkshopId,
            entity.IsActive,
            entity.SortOrder,
            DeserializeList(entity.DependencyModIdsJson),
            DeserializeList(entity.MapFoldersJson));

    private static ModsMapsMapRowDto ToDto(ModsMapsDraftMapRowEntity entity) =>
        new(
            entity.RowId,
            entity.Title,
            entity.MapFolder,
            entity.WorkshopId,
            entity.IsActive,
            entity.SortOrder);

    private static ModsMapsEditorMode ParseEditorMode(string value) =>
        Enum.TryParse<ModsMapsEditorMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : ModsMapsEditorMode.Browse;

    private static IReadOnlyList<string> DeserializeList(string json)
    {
        try
        {
            return DistinctNonEmpty(JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? []);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> DistinctNonEmpty(IEnumerable<string> values)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var candidate = value?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                normalized.Add(candidate);
            }
        }

        return normalized;
    }
}
