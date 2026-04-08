using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Core.Settings;

public interface ISettingsCatalogResolver
{
    StructuredSettingsCatalog Resolve(ProjectZomboidBranch branch);
}
