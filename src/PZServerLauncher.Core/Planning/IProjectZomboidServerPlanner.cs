using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Core.Planning;

public interface IProjectZomboidServerPlanner
{
    SteamCmdScriptPlan CreateInstallScript(ServerProfile profile);

    ServerLaunchPlan CreateLaunchPlan(ServerProfile profile);

    ServerPaths ResolvePaths(ServerProfile profile);
}
