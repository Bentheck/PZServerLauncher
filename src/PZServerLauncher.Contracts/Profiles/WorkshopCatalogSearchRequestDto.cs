namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogSearchRequestDto(
    string Query,
    WorkshopCatalogSearchMode SearchMode,
    int Take = 12,
    PZServerLauncher.Core.Profiles.WorkshopPreset? CurrentPreset = null,
    WorkshopCatalogSearchFilter SearchFilter = WorkshopCatalogSearchFilter.All,
    IReadOnlyList<string>? SelectedTags = null);
