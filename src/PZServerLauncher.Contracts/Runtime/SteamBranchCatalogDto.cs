namespace PZServerLauncher.Contracts.Runtime;

public sealed record SteamBranchCatalogDto(
    IReadOnlyList<SteamBranchDto> Branches,
    string SelectedBranch,
    DateTimeOffset RefreshedAtUtc);

public sealed record SteamBranchDto(
    string Name,
    string DisplayName,
    string BuildId,
    bool RequiresPassword,
    bool IsDefault);
