namespace PZServerLauncher.Host.Infrastructure;

internal static class FileSystemCleanup
{
    private const FileAttributes AttributesToClear =
        FileAttributes.ReadOnly |
        FileAttributes.Hidden |
        FileAttributes.System;

    public static bool DeleteDirectoryIfExists(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        NormalizeDirectoryAttributes(directory);
        Directory.Delete(directory, recursive: true);
        return true;
    }

    public static bool DeleteFileIfExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        NormalizePathAttributes(path);
        File.Delete(path);
        return true;
    }

    private static void NormalizeDirectoryAttributes(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            NormalizePathAttributes(file);
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            NormalizePathAttributes(childDirectory);
        }

        NormalizePathAttributes(directory);
    }

    private static void NormalizePathAttributes(string path)
    {
        var currentAttributes = File.GetAttributes(path);
        var normalizedAttributes = currentAttributes & ~AttributesToClear;
        if (normalizedAttributes != currentAttributes)
        {
            File.SetAttributes(path, normalizedAttributes);
        }
    }
}
