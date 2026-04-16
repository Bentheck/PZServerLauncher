namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopCatalogSearchResultDto(
    string Query,
    WorkshopCatalogSearchMode SearchMode,
    bool HasSteamWebApiKeyConfigured,
    IReadOnlyList<WorkshopCatalogItemDto> Results,
    IReadOnlyList<string> Diagnostics);
