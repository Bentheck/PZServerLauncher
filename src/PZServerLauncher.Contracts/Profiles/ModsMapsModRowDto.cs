namespace PZServerLauncher.Contracts.Profiles;

public sealed record ModsMapsModRowDto(
    int RowId,
    string ModName,
    string ModId,
    string WorkshopId,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<string> DependencyModIds,
    IReadOnlyList<string> MapFolders);
