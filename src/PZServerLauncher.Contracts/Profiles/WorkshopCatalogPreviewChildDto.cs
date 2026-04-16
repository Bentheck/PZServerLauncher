namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogPreviewChildDto(
    string WorkshopId,
    string Title,
    bool IsInstalledLocally,
    bool IsQueued);
