namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogItemDto(
    string WorkshopId,
    string Title,
    string Description,
    string? PreviewImageUrl,
    WorkshopCatalogItemSource Source,
    bool IsInstalledLocally,
    bool IsQueued,
    IReadOnlyList<string> ModIds,
    IReadOnlyList<string> MapFolders,
    IReadOnlyList<string>? DependencyModIds = null,
    IReadOnlyList<string>? Tags = null,
    WorkshopCatalogItemKind Kind = WorkshopCatalogItemKind.Item,
    int CollectionItemCount = 0,
    IReadOnlyList<string>? CollectionChildWorkshopIds = null);
