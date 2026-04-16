namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogPreviewDto(
    WorkshopCatalogItemDto Item,
    IReadOnlyList<string> WorkshopItemIdsToAdd,
    IReadOnlyList<string> ModIdsToAdd,
    IReadOnlyList<string> MapFoldersToAdd,
    IReadOnlyList<WorkshopCatalogPreviewChildDto>? CollectionChildren = null);
