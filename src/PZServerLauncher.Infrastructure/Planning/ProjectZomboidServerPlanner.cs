using System.Globalization;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Infrastructure.Planning;

public sealed class ProjectZomboidServerPlanner : IProjectZomboidServerPlanner
{
    public SteamCmdScriptPlan CreateInstallScript(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var lines = new List<string>
        {
            "@ShutdownOnFailedCommand 1",
            "@NoPromptForPassword 1",
            $"force_install_dir {QuoteForSteamCmd(profile.InstallDirectory)}",
            "login anonymous",
            GetAppUpdateCommand(profile.Branch),
            "quit",
        };

        return new SteamCmdScriptPlan(profile.InstallDirectory, lines);
    }

    public ServerLaunchPlan CreateLaunchPlan(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var launcherFileName = profile.UseSteam
            ? ProjectZomboidDefaults.StableBatchFileName
            : ProjectZomboidDefaults.NonSteamBatchFileName;

        var arguments = new List<string>
        {
            "-cachedir",
            profile.CacheDirectory,
            "-servername",
            profile.ServerName,
            "-port",
            profile.DefaultPort.ToString(CultureInfo.InvariantCulture),
            "-udpport",
            profile.UdpPort.ToString(CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(profile.AdminUsername))
        {
            arguments.Add("-adminusername");
            arguments.Add(profile.AdminUsername);
        }

        if (!string.IsNullOrWhiteSpace(profile.AdminPassword))
        {
            arguments.Add("-adminpassword");
            arguments.Add(profile.AdminPassword);
        }

        if (!string.IsNullOrWhiteSpace(profile.BindIp))
        {
            arguments.Add("-ip");
            arguments.Add(profile.BindIp);
        }

        var notes =
            $"Use {profile.PreferredMemoryInGigabytes} GB as the first launcher preset target. " +
            "The dedicated server batch file still needs a memory-management pass in the next milestone.";

        return new ServerLaunchPlan(
            WorkingDirectory: profile.InstallDirectory,
            LauncherPath: Path.Combine(profile.InstallDirectory, launcherFileName),
            Arguments: arguments,
            Notes: notes);
    }

    public ServerPaths ResolvePaths(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var serverDirectory = Path.Combine(profile.CacheDirectory, "Server");
        var savesDirectory = Path.Combine(profile.CacheDirectory, "Saves", "Multiplayer");
        var worldDirectory = Path.Combine(savesDirectory, profile.ServerName);

        return new ServerPaths(
            CacheRootDirectory: profile.CacheDirectory,
            ServerConfigDirectory: serverDirectory,
            SavesDirectory: savesDirectory,
            WorldDirectory: worldDirectory,
            IniFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}.ini"),
            SandboxVarsFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}_SandboxVars.lua"),
            SpawnRegionsFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}_spawnregions.lua"),
            SpawnPointsFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}_spawnpoints.lua"));
    }

    public string FormatLaunchCommand(ServerLaunchPlan plan) =>
        CommandLineFormatter.Format(plan.LauncherPath, plan.Arguments);

    public string FormatSteamCmdScript(SteamCmdScriptPlan plan) =>
        string.Join(Environment.NewLine, plan.ScriptLines);

    private static string GetAppUpdateCommand(ProjectZomboidBranch branch) =>
        branch switch
        {
            ProjectZomboidBranch.Stable41 => $"app_update {ProjectZomboidDefaults.DedicatedServerAppId} validate",
            ProjectZomboidBranch.Unstable42 => $"app_update {ProjectZomboidDefaults.DedicatedServerAppId} -beta unstable validate",
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unsupported Project Zomboid branch."),
        };

    private static string QuoteForSteamCmd(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
