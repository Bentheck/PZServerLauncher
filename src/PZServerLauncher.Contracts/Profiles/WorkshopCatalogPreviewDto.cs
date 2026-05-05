namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogPreviewDto(
    WorkshopCatalogItemDto Item,
    IReadOnlyList<string> WorkshopItemIdsToAdd,
    IReadOnlyList<string> ModIdsToAdd,
    IReadOnlyList<string> MapFoldersToAdd,
    IReadOnlyList<WorkshopCatalogPreviewChildDto>? CollectionChildren = null,
    IReadOnlyList<WorkshopCatalogPreviewChildDto>? DependencyChildren = null,
    IReadOnlyList<string>? DependencyWorkshopItemIdsToAdd = null,
    IReadOnlyList<string>? DependencyModIdsToAdd = null,
    IReadOnlyList<string>? DependencyMapFoldersToAdd = null,
    IReadOnlyDictionary<string, string>? ModNamesById = null);
