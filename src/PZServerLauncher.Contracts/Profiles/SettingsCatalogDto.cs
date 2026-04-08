using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsCatalogDto(
    string CatalogId,
    int CatalogVersion,
    ProjectZomboidBranch Branch,
    IReadOnlyList<SettingsPageDto> Pages);
