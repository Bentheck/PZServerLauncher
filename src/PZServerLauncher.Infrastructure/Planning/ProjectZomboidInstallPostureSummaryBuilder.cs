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

        var deploymentPostureSummary = !installDetected
            ? "This profile still needs its first dedicated-server install. SteamCMD will create the branch footprint, then the cache/config layer can be initialized on first launch."
            : string.Equals(runtimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "The branch footprint is present and the server is live. Treat update work as a maintenance operation so the runtime can stop cleanly and pre-update safety backup can complete."
                : usesDirectJava
                    ? "The branch footprint, cache layer, and launcher-managed Java template are aligned for a predictable deployment cycle."
                    : "The branch footprint is present, but launch still falls back to the vendor batch script. Deployment remains viable, but runtime tuning is less deterministic.";

        var maintenanceWindowSummary = string.Equals(runtimeState, "Running", StringComparison.OrdinalIgnoreCase)
            ? "Maintenance window required: stop or restart traffic before queueing update work so SteamCMD and the pre-update backup can run against a quiet server."
            : hasBackup
                ? $"Maintenance window ready: the server is idle and the latest backup is {latestBackup}."
                : "Maintenance window ready: the server is idle, but capture a manual backup if you want a human-reviewed recovery point before update.";

        var branchIsolationSummary = profile.Branch switch
        {
            ProjectZomboidBranch.Stable41 => "Stable 41 should keep its own install root and cache root so it does not trample unstable 42 config, saves, or Java launch assumptions.",
            ProjectZomboidBranch.Unstable42 => "Unstable 42 should stay isolated from stable 41 so beta binaries, cache content, and structured settings do not bleed across branches.",
            _ => "Each Project Zomboid branch should keep isolated install and cache roots to avoid mixed binaries and config drift.",
        };

        var operatorSequenceSummary = !installDetected
            ? "Recommended sequence: 1. Install this branch. 2. Start once to initialize cache/config. 3. Review General and Sandbox. 4. Capture a manual backup. 5. Start the live server."
            : string.Equals(runtimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "Recommended sequence: 1. Announce maintenance. 2. Capture or confirm backup posture. 3. Stop or restart the live runtime. 4. Queue update. 5. Review logs after restart."
                : "Recommended sequence: 1. Review preflight and branch isolation. 2. Confirm backup posture. 3. Queue install/update. 4. Review launch preview. 5. Start and watch live logs.";

        var preflightChecks = new List<string>
        {
            $"Install root: {(installDetected ? "ready" : "missing")} | {profile.InstallDirectory}",
            $"Cache root: {(cacheDetected ? "ready" : "missing")} | {profile.CacheDirectory}",
            $"Launcher script: {(launcherDetected ? "present" : "missing")} | {Path.GetFileName(expectedLauncherPath)}",
            $"Config footprint: {(configDirectoryDetected ? "config root ready" : "config root missing")} | {(iniDetected ? "INI present" : "INI missing")} | {(sandboxDetected ? "Sandbox present" : "Sandbox missing")}",
            $"World state: {(worldDetected ? "world save detected" : "world save not created yet")}",
            $"Backup posture: {(hasBackup ? $"latest backup {latestBackup}" : "no backup archive yet")}",
            $"Runtime window: {runtimeState}",
            $"Launch mode: {(usesDirectJava ? "direct Java template" : "vendor batch fallback")}",
        };

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
            usesDirectJava,
            deploymentPostureSummary,
            maintenanceWindowSummary,
            branchIsolationSummary,
            operatorSequenceSummary,
            preflightChecks);
    }
}
