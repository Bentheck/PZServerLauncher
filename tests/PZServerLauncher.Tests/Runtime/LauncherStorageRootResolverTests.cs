using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Runtime;

public sealed class LauncherStorageRootResolverTests
{
    [Fact]
    public void Resolve_UsesInstallRootWhenAppAndHostFoldersExist()
    {
        var installRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
        var appDirectory = Path.Combine(installRoot, "App");
        var hostDirectory = Path.Combine(installRoot, "Host");

        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(hostDirectory);

        try
        {
            var resolved = LauncherStorageRootResolver.Resolve(appDirectory);

            Assert.Equal(Path.GetFullPath(installRoot), resolved);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_UsesInstallRootWhenDesktopAppIsInstalledDirectlyAtRoot()
    {
        var installRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(installRoot);
        File.WriteAllText(Path.Combine(installRoot, "PZServerLauncher.App.exe"), string.Empty);

        try
        {
            var resolved = LauncherStorageRootResolver.Resolve(installRoot);

            Assert.Equal(Path.GetFullPath(installRoot), resolved);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_UsesSolutionRootWhenRunningFromProjectBuildOutput()
    {
        var solutionRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
        var buildOutputDirectory = Path.Combine(solutionRoot, "src", "PZServerLauncher.App", "bin", "Debug", "net10.0");

        Directory.CreateDirectory(buildOutputDirectory);
        File.WriteAllText(Path.Combine(solutionRoot, "PZServerLauncher.sln"), "placeholder");

        try
        {
            var resolved = LauncherStorageRootResolver.Resolve(buildOutputDirectory);

            Assert.Equal(Path.GetFullPath(solutionRoot), resolved);
        }
        finally
        {
            Directory.Delete(solutionRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_UsesExplicitEnvironmentOverrideWhenProvided()
    {
        var originalValue = Environment.GetEnvironmentVariable(LauncherStorageRootResolver.RootOverrideEnvironmentVariable);
        var overrideRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(overrideRoot);
        Environment.SetEnvironmentVariable(LauncherStorageRootResolver.RootOverrideEnvironmentVariable, overrideRoot);

        try
        {
            var resolved = LauncherStorageRootResolver.Resolve();

            Assert.Equal(Path.GetFullPath(overrideRoot), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LauncherStorageRootResolver.RootOverrideEnvironmentVariable, originalValue);
            Directory.Delete(overrideRoot, recursive: true);
        }
    }
}
