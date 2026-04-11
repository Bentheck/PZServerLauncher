namespace PZServerLauncher.Core.Runtime;

public static class LauncherStorageRootResolver
{
    public const string RootOverrideEnvironmentVariable = "PZSERVERLAUNCHER_ROOT";

    public static string Resolve(string? baseDirectory = null)
    {
        var overrideDirectory = Environment.GetEnvironmentVariable(RootOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return Path.GetFullPath(overrideDirectory);
        }

        var probeDirectory = new DirectoryInfo(Path.GetFullPath(string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory));

        for (var current = probeDirectory; current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "App")) &&
                Directory.Exists(Path.Combine(current.FullName, "Host")))
            {
                return current.FullName;
            }

            if (File.Exists(Path.Combine(current.FullName, "PZServerLauncher.sln")))
            {
                return current.FullName;
            }
        }

        return probeDirectory.FullName;
    }
}
