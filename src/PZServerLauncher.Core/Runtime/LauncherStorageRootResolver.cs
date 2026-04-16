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
        var solutionRoot = FindAncestor(probeDirectory, static current =>
            File.Exists(Path.Combine(current.FullName, "PZServerLauncher.sln")));
        if (solutionRoot is not null)
        {
            return solutionRoot.FullName;
        }

        for (var current = probeDirectory; current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "App")) &&
                Directory.Exists(Path.Combine(current.FullName, "Host")))
            {
                return current.FullName;
            }

            if (string.Equals(current.Name, "App", StringComparison.OrdinalIgnoreCase) &&
                (File.Exists(Path.Combine(current.FullName, "PZServerLauncher.App.exe")) ||
                 File.Exists(Path.Combine(current.FullName, "PZServerLauncher.App.dll"))))
            {
                return current.Parent?.FullName ?? current.FullName;
            }

            if (ContainsDesktopAppBinary(current))
            {
                return current.FullName;
            }
        }

        return probeDirectory.FullName;
    }

    private static DirectoryInfo? FindAncestor(DirectoryInfo start, Func<DirectoryInfo, bool> predicate)
    {
        for (var current = start; current is not null; current = current.Parent)
        {
            if (predicate(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool ContainsDesktopAppBinary(DirectoryInfo directory) =>
        File.Exists(Path.Combine(directory.FullName, "PZServerLauncher.App.exe")) ||
        File.Exists(Path.Combine(directory.FullName, "PZServerLauncher.App.dll"));
}
