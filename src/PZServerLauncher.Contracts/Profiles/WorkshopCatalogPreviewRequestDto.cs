namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogPreviewRequestDto(
    WorkshopCatalogSearchMode SearchMode,
    PZServerLauncher.Core.Profiles.WorkshopPreset? CurrentPreset = null);
