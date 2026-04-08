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
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            CacheDirectory = @"D:\Servers\Profiles\bravo server",
            AdminPassword = "secret-password",
        };

        var plan = _planner.CreateLaunchPlan(profile);
        var commandLine = _planner.FormatLaunchCommand(plan);

        Assert.Contains("-cachedir", plan.Arguments);
        Assert.Contains("-servername", plan.Arguments);
        Assert.Contains("-adminpassword", plan.Arguments);
        Assert.Contains("\"D:\\Servers\\Profiles\\bravo server\"", commandLine);
        Assert.Contains("secret-password", commandLine);
    }
}
