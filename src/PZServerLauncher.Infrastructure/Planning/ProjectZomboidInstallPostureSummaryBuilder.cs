using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Infrastructure.Planning;

public static class ProjectZomboidInstallPostureSummaryBuilder
{
    public static ProjectZomboidInstallPostureSummary Build(ServerProfile profile, string runtimeState, bool hasBackup, string latestBackup)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        var expectedLauncherPath = Path.Combine(
            profile.InstallDirectory,
            profile.UseSteam ? ProjectZomboidDefaults.StableBatchFileName : ProjectZomboidDefaults.NonSteamBatchFileName);

        var installDetected = Directory.Exists(profile.InstallDirectory) || Directory.Exists(Path.Combine(profile.InstallDirectory, "server"));
        var cacheDetected = Directory.Exists(profile.CacheDirectory);
        var launcherDetected = File.Exists(expectedLauncherPath);
        var configDirectoryDetected = Directory.Exists(paths.ServerConfigDirectory);
        var iniDetected = File.Exists(paths.IniFilePath);
        var sandboxDetected = File.Exists(paths.SandboxVarsFilePath);
        var worldDetected = Directory.Exists(paths.WorldDirectory);

        var launchPlan = planner.CreateLaunchPlan(profile);
        var usesDirectJava = launchPlan.Strategy == ServerLaunchStrategy.DirectJavaTemplate;
        var installScript = planner.CreateInstallScript(profile);
        var steamCmdScriptPreview = planner.FormatSteamCmdScript(installScript);
        var launchCommandPreview = planner.FormatLaunchCommand(launchPlan);

        var branchChannelSummary = profile.Branch switch
        {
            ProjectZomboidBranch.Stable41 => $"Build 41 Stable | Steam app {ProjectZomboidDefaults.DedicatedServerAppId} standard channel",
            ProjectZomboidBranch.Unstable42 => $"Build 42 Unstable | Steam app {ProjectZomboidDefaults.DedicatedServerAppId} beta unstable",
            _ => $"Branch {profile.Branch} | Steam app {ProjectZomboidDefaults.DedicatedServerAppId}",
        };

        var steamCmdCommandSummary = profile.Branch switch
        {
            ProjectZomboidBranch.Stable41 => $"SteamCMD: app_update {ProjectZomboidDefaults.DedicatedServerAppId} validate",
            ProjectZomboidBranch.Unstable42 => $"SteamCMD: app_update {ProjectZomboidDefaults.DedicatedServerAppId} -beta unstable validate",
            _ => $"SteamCMD app {ProjectZomboidDefaults.DedicatedServerAppId}",
        };

        var installFootprintSummary = !installDetected
            ? "Install root is missing. Queue Install to lay down the dedicated-server footprint."
            : launcherDetected
                ? $"Install root detected and {Path.GetFileName(expectedLauncherPath)} is present."
                : $"Install root exists, but {Path.GetFileName(expectedLauncherPath)} is missing.";

        var cacheFootprintSummary = !cacheDetected
            ? "Cache root is missing. The server has not initialized local config, saves, or backup data yet."
            : configDirectoryDetected
                ? iniDetected && sandboxDetected
                    ? "Cache root, config directory, and the core server config files are present."
                    : "Cache root and config directory exist, but one or more core config files are still missing."
                : "Cache root exists, but the server config directory has not been initialized yet.";

        var launchReadinessSummary = !launcherDetected
            ? "Launch readiness is blocked because the expected launcher script is missing."
            : usesDirectJava
                ? $"Direct Java template is ready with launcher-managed memory set to {profile.PreferredMemoryInGigabytes} GB."
                : "Vendor batch fallback is active. The server can still launch, but memory remains vendor-managed until the Java template can be extracted.";

        var runtimePolicySummary = $"{runtimeState} | start with host {(profile.StartWithHost ? "on" : "off")} | auto-restart {(profile.AutoRestartOnCrash ? "on" : "off")} | preferred memory {profile.PreferredMemoryInGigabytes} GB";

        var backupSafetySummary = hasBackup
            ? $"Latest backup: {latestBackup}. Updates still trigger a pre-update safety backup automatically."
            : "No backup archive exists yet. Updates trigger a pre-update backup automatically, but taking a manual backup now is safer.";

        var preflightSummary = !installDetected
            ? "Install root missing. Run Install before you try to update this branch."
            : string.Equals(runtimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "Server is currently running. Plan a maintenance window before you queue an update."
                : !cacheDetected
                    ? "Install root exists, but the local cache footprint is not ready yet. Start the server once or import an existing cache before deeper config work."
                    : !configDirectoryDetected || !iniDetected || !sandboxDetected
                        ? "Dedicated-server files are present, but the cache/config footprint is only partially initialized."
                        : usesDirectJava
                            ? "Install, config, and launch template look ready for a clean update cycle."
                            : "Install and config look ready, but launch will currently use vendor batch fallback.";

        return new ProjectZomboidInstallPostureSummary(
            branchChannelSummary,
            steamCmdCommandSummary,
            steamCmdScriptPreview,
            launchCommandPreview,
            expectedLauncherPath,
            installFootprintSummary,
            cacheFootprintSummary,
            launchReadinessSummary,
            runtimePolicySummary,
            backupSafetySummary,
            preflightSummary,
            installDetected,
            cacheDetected,
            launcherDetected,
            configDirectoryDetected,
            iniDetected,
            sandboxDetected,
            worldDetected,
            usesDirectJava);
    }
}
