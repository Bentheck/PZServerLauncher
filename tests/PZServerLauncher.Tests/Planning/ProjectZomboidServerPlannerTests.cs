using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Tests.Planning;

public sealed class ProjectZomboidServerPlannerTests
{
    private readonly ProjectZomboidServerPlanner _planner = new();

    [Fact]
    public void CreateInstallScript_UsesUnstableBranchCommandForBuild42()
    {
        var profile = ServerProfileFactory.CreateStarterProfile();

        var plan = _planner.CreateInstallScript(profile);

        Assert.Contains("app_update 380870 -beta unstable validate", plan.ScriptLines);
    }

    [Fact]
    public void ResolvePaths_UsesCachedirAndServerNameForConfigAndWorldLocations()
    {
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            CacheDirectory = @"D:\Servers\Profiles\alpha",
            ServerName = "alpha42",
        };

        var paths = _planner.ResolvePaths(profile);

        Assert.Equal(@"D:\Servers\Profiles\alpha\Server\alpha42.ini", paths.IniFilePath);
        Assert.Equal(@"D:\Servers\Profiles\alpha\Server\alpha42_SandboxVars.lua", paths.SandboxVarsFilePath);
        Assert.Equal(@"D:\Servers\Profiles\alpha\Saves\Multiplayer\alpha42", paths.WorldDirectory);
    }

    [Fact]
    public void CreateLaunchPlan_IncludesServerSpecificArguments()
    {
        var installDirectory = CreateInstallDirectory(
            """
            @echo off
            setlocal
            set "JAVA_HOME=%~dp0jre64"
            "%JAVA_HOME%\bin\java.exe" ^
              -Dzomboid.steam=1 ^
              -Djava.awt.headless=true ^
              -Xms2048m ^
              -Xmx2048m ^
              -cp "%~dp0zombie.jar;%~dp0lib\*" ^
              zombie.network.GameServer ^
              -cachedir "%UserProfile%\Zomboid" ^
              -servername servertest
            """);

        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            InstallDirectory = installDirectory,
            CacheDirectory = @"D:\Servers\Profiles\bravo server",
            AdminPassword = "secret-password",
            PreferredMemoryInGigabytes = 8,
        };

        try
        {
            var plan = _planner.CreateLaunchPlan(profile);
            var commandLine = _planner.FormatLaunchCommand(plan);

            Assert.Equal(ServerLaunchStrategy.DirectJavaTemplate, plan.Strategy);
            Assert.EndsWith(Path.Combine("jre64", "bin", "java.exe"), plan.LauncherPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("-Xms8g", plan.Arguments);
            Assert.Contains("-Xmx8g", plan.Arguments);
            Assert.DoesNotContain("-Xms2048m", plan.Arguments);
            Assert.DoesNotContain("-Xmx2048m", plan.Arguments);
            Assert.Contains(plan.Arguments, argument => argument.StartsWith("-cachedir=", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("-servername", plan.Arguments);
            Assert.Contains("-adminpassword", plan.Arguments);
            Assert.Contains("-cachedir=D:\\Servers\\Profiles\\bravo server", commandLine, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret-password", commandLine);
            Assert.DoesNotContain("servertest", commandLine, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(installDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateLaunchPlan_FallsBackToVendorBatchWhenTemplateCannotBeParsed()
    {
        var installDirectory = CreateInstallDirectory(
            """
            @echo off
            echo unsupported launcher
            """);

        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            InstallDirectory = installDirectory,
        };

        try
        {
            var plan = _planner.CreateLaunchPlan(profile);

            Assert.Equal(ServerLaunchStrategy.VendorBatchFallback, plan.Strategy);
            Assert.EndsWith("StartServer64.bat", plan.LauncherPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Falling back", plan.Notes, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(plan.Arguments, argument => argument.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(installDirectory, recursive: true);
        }
    }

    private static string CreateInstallDirectory(string batchFileContent)
    {
        var installDirectory = Path.Combine(Path.GetTempPath(), $"pz-launch-plan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(Path.Combine(installDirectory, "jre64", "bin"));
        File.WriteAllText(Path.Combine(installDirectory, "jre64", "bin", "java.exe"), string.Empty);
        File.WriteAllText(Path.Combine(installDirectory, "StartServer64.bat"), batchFileContent);
        return installDirectory;
    }
}
