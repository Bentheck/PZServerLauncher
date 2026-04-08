using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Core.Settings;

public sealed record StructuredSettingsCatalog(
    string CatalogId,
    int CatalogVersion,
    ProjectZomboidBranch Branch,
    IReadOnlyList<StructuredPageDefinition> Pages);
