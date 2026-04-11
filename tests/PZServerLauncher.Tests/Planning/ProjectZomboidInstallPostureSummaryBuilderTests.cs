using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Planning;

public sealed class ProjectZomboidInstallPostureSummaryBuilderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_ReportsStableDirectJavaReadinessWhenFootprintExists()
    {
        var profile = CreateProfile("stable-ready", ProjectZomboidBranch.Stable41);
        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);

        Directory.CreateDirectory(profile.InstallDirectory);
        Directory.CreateDirectory(Path.Combine(profile.InstallDirectory, "jre64", "bin"));
        File.WriteAllText(Path.Combine(profile.InstallDirectory, "jre64", "bin", "java.exe"), string.Empty);
        File.WriteAllText(
            Path.Combine(profile.InstallDirectory, ProjectZomboidDefaults.StableBatchFileName),
            """
            @echo off
            set JAVA="%~dp0jre64\bin\java.exe"
            %JAVA% -Xms1024m -Xmx1024m -Djava.library.path="./" -cp lwjgl.jar;lwjgl_util.jar;./ zombie.network.GameServer
            """);

        Directory.CreateDirectory(paths.ServerConfigDirectory);
        Directory.CreateDirectory(paths.WorldDirectory);
        File.WriteAllText(paths.IniFilePath, "Public=true");
        File.WriteAllText(paths.SandboxVarsFilePath, "SandboxVars = {\n    VERSION = 4,\n}");

        var summary = ProjectZomboidInstallPostureSummaryBuilder.Build(profile, "Stopped", hasBackup: true, latestBackup: "stable-backup.zip");

        Assert.True(summary.InstallDetected);
        Assert.True(summary.CacheDetected);
        Assert.True(summary.LauncherDetected);
        Assert.True(summary.ConfigDirectoryDetected);
        Assert.True(summary.IniDetected);
        Assert.True(summary.SandboxDetected);
        Assert.True(summary.WorldDetected);
        Assert.True(summary.UsesDirectJavaTemplate);
        Assert.Contains("Build 41 Stable", summary.BranchChannelSummary);
        Assert.Contains("app_update 380870 validate", summary.SteamCmdCommandSummary);
        Assert.Contains("@ShutdownOnFailedCommand 1", summary.SteamCmdScriptPreview);
        Assert.Contains("force_install_dir", summary.SteamCmdScriptPreview);
        Assert.Contains("-servername", summary.LaunchCommandPreview);
        Assert.Contains("launcher-managed memory", summary.LaunchReadinessSummary);
        Assert.Contains("core server config files are present", summary.CacheFootprintSummary);
        Assert.Contains("Latest backup: stable-backup.zip", summary.BackupSafetySummary);
        Assert.Contains("look ready for a clean update cycle", summary.PreflightSummary);
        Assert.Contains("launcher-managed Java template", summary.DeploymentPostureSummary);
        Assert.Contains("Stable 41", summary.BranchIsolationSummary);
        Assert.Contains("Recommended sequence", summary.OperatorSequenceSummary);
        Assert.Contains(summary.PreflightChecks, check => check.Contains("Install root:", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ReportsUnstableLaunchBlockedWhenLauncherTemplateCannotBeExtracted()
    {
        var profile = CreateProfile("unstable-fallback", ProjectZomboidBranch.Unstable42);

        Directory.CreateDirectory(profile.InstallDirectory);
        File.WriteAllText(
            Path.Combine(profile.InstallDirectory, ProjectZomboidDefaults.StableBatchFileName),
            """
            @echo off
            echo launcher exists but no java template can be extracted
            """);
        Directory.CreateDirectory(profile.CacheDirectory);

        var summary = ProjectZomboidInstallPostureSummaryBuilder.Build(profile, "Stopped", hasBackup: false, latestBackup: "No backups");

        Assert.True(summary.InstallDetected);
        Assert.True(summary.CacheDetected);
        Assert.True(summary.LauncherDetected);
        Assert.False(summary.UsesDirectJavaTemplate);
        Assert.Contains("Build 42 Unstable", summary.BranchChannelSummary);
        Assert.Contains("-beta unstable", summary.SteamCmdCommandSummary);
        Assert.Contains("-beta unstable validate", summary.SteamCmdScriptPreview);
        Assert.Contains("Launch blocked", summary.LaunchCommandPreview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Launch blocked", summary.LaunchReadinessSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No backup archive exists yet", summary.BackupSafetySummary);
        Assert.Contains("launch is blocked", summary.DeploymentPostureSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unstable 42", summary.BranchIsolationSummary);
    }

    [Fact]
    public void Build_ReportsMissingInstallAndCacheBeforeBootstrap()
    {
        var profile = CreateProfile("missing-footprint", ProjectZomboidBranch.Unstable42);

        var summary = ProjectZomboidInstallPostureSummaryBuilder.Build(profile, "Stopped", hasBackup: false, latestBackup: "No backups");

        Assert.False(summary.InstallDetected);
        Assert.False(summary.CacheDetected);
        Assert.False(summary.LauncherDetected);
        Assert.False(summary.ConfigDirectoryDetected);
        Assert.False(summary.IniDetected);
        Assert.False(summary.SandboxDetected);
        Assert.False(summary.WorldDetected);
        Assert.Contains("Install root is missing", summary.InstallFootprintSummary);
        Assert.Contains("Cache root is missing", summary.CacheFootprintSummary);
        Assert.Contains("blocked because the expected launcher script is missing", summary.LaunchReadinessSummary);
        Assert.Contains("Run Install before you try to update", summary.PreflightSummary);
        Assert.Contains("first dedicated-server install", summary.DeploymentPostureSummary);
        Assert.Contains("Recommended sequence", summary.OperatorSequenceSummary);
        Assert.EndsWith(ProjectZomboidDefaults.StableBatchFileName, summary.ExpectedLauncherPath);
    }

    [Fact]
    public void Build_ReportsMaintenanceWindowWhenServerIsRunning()
    {
        var profile = CreateProfile("running-maintenance", ProjectZomboidBranch.Stable41);
        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);

        Directory.CreateDirectory(profile.InstallDirectory);
        File.WriteAllText(
            Path.Combine(profile.InstallDirectory, ProjectZomboidDefaults.StableBatchFileName),
            """
            @echo off
            echo launcher exists but no java template can be extracted
            """);
        Directory.CreateDirectory(paths.ServerConfigDirectory);
        File.WriteAllText(paths.IniFilePath, "Public=true");
        File.WriteAllText(paths.SandboxVarsFilePath, "SandboxVars = {\n    VERSION = 4,\n}");

        var summary = ProjectZomboidInstallPostureSummaryBuilder.Build(profile, "Running", hasBackup: true, latestBackup: "pre-maintenance.zip");

        Assert.Contains("maintenance operation", summary.DeploymentPostureSummary);
        Assert.Contains("Maintenance window required", summary.MaintenanceWindowSummary);
        Assert.Contains("Announce maintenance", summary.OperatorSequenceSummary);
        Assert.Contains(summary.PreflightChecks, check => check.Contains("Runtime window: Running", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_DoesNotTreatNestedLauncherAsConfiguredInstallRootLauncher()
    {
        var profile = CreateProfile("nested-launcher", ProjectZomboidBranch.Stable41);
        var nestedInstallDirectory = Path.Combine(profile.InstallDirectory, "Project Zomboid Dedicated Server");

        Directory.CreateDirectory(nestedInstallDirectory);
        File.WriteAllText(
            Path.Combine(nestedInstallDirectory, ProjectZomboidDefaults.StableBatchFileName),
            """
            @echo off
            echo launcher exists but no java template can be extracted
            """);

        var summary = ProjectZomboidInstallPostureSummaryBuilder.Build(profile, "Stopped", hasBackup: false, latestBackup: "No backups");

        Assert.False(summary.LauncherDetected);
        Assert.EndsWith(ProjectZomboidDefaults.StableBatchFileName, summary.ExpectedLauncherPath, StringComparison.OrdinalIgnoreCase);
    }

    private ServerProfile CreateProfile(string id, ProjectZomboidBranch branch) =>
        ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = id,
            DisplayName = id,
            ServerName = "servertest",
            Branch = branch,
            InstallDirectory = Path.Combine(_tempRoot, id, "install"),
            CacheDirectory = Path.Combine(_tempRoot, id, "cache"),
            PreferredMemoryInGigabytes = 10,
        };

    public void Dispose()
    {
        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
