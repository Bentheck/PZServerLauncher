namespace PZServerLauncher.Contracts.Profiles;

public sealed record ModsMapsMapRowDto(
    int RowId,
    string Title,
    string MapFolder,
    string WorkshopId,
    bool IsActive,
    int SortOrder);
