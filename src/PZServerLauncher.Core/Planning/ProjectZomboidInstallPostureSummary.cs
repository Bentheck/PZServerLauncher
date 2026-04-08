namespace PZServerLauncher.Core.Planning;

public sealed record ProjectZomboidInstallPostureSummary(
    string BranchChannelSummary,
    string SteamCmdCommandSummary,
    string ExpectedLauncherPath,
    string InstallFootprintSummary,
    string CacheFootprintSummary,
    string LaunchReadinessSummary,
    string RuntimePolicySummary,
    string BackupSafetySummary,
    string PreflightSummary,
    bool InstallDetected,
    bool CacheDetected,
    bool LauncherDetected,
    bool ConfigDirectoryDetected,
    bool IniDetected,
    bool SandboxDetected,
    bool WorldDetected,
    bool UsesDirectJavaTemplate);
