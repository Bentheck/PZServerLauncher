namespace PZServerLauncher.Core.Profiles;

public static class ProjectZomboidBranchSupport
{
    public const ProjectZomboidBranch CurrentBranch = ProjectZomboidBranch.Unstable42;
    public const string CurrentCatalogId = "pz.settings.b42";
    public const int CurrentCatalogVersion = 4;
    public const string CurrentFieldPrefix = "b42";

    public static ProjectZomboidBranch Normalize(ProjectZomboidBranch branch) => CurrentBranch;

    public static ProjectZomboidBranch FromPersistedValue(int value) =>
        Enum.IsDefined(typeof(ProjectZomboidBranch), value)
            ? Normalize((ProjectZomboidBranch)value)
            : CurrentBranch;
}
