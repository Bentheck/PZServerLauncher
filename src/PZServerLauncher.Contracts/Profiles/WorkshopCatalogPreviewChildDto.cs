namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogPreviewChildDto(
    string WorkshopId,
    string Title,
    bool IsInstalledLocally,
    bool IsQueued,
    IReadOnlyList<string>? ModIds = null,
    IReadOnlyList<string>? MapFolders = null,
    IReadOnlyList<string>? DependencyModIds = null);
