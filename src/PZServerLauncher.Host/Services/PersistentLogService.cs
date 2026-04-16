using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class PersistentLogService(AppPaths appPaths) : IRuntimeLogSink
{
    private readonly RollingFileLogWriter _hostWriter = new(Path.Combine(appPaths.LogsDirectory, "host.log"));
    private readonly Dictionary<string, RollingFileLogWriter> _profileWriters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _profileWritersGate = new();

    public void WriteHostLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _hostWriter.WriteLine(line);
    }

    public void WriteProfileLine(string profileId, string line)
    {
        if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var writer = GetProfileWriter(profileId);
        writer.WriteLine(line);
    }

    public bool DeleteProfileLogs(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return false;
        }

        lock (_profileWritersGate)
        {
            _profileWriters.Remove(profileId);
        }

        var deletedAny = false;
        foreach (var path in EnumerateProfileLogPaths(profileId))
        {
            deletedAny |= FileSystemCleanup.DeleteFileIfExists(path);
        }

        return deletedAny;
    }

    private RollingFileLogWriter GetProfileWriter(string profileId)
    {
        lock (_profileWritersGate)
        {
            if (_profileWriters.TryGetValue(profileId, out var writer))
            {
                return writer;
            }

            writer = new RollingFileLogWriter(GetProfileLogPath(profileId));
            _profileWriters[profileId] = writer;
            return writer;
        }
    }

    private string GetProfileLogPath(string profileId) =>
        Path.Combine(appPaths.LogsDirectory, "profiles", $"{SanitizeProfileId(profileId)}.log");

    private IEnumerable<string> EnumerateProfileLogPaths(string profileId)
    {
        var basePath = GetProfileLogPath(profileId);
        yield return basePath;

        for (var archiveIndex = 1; archiveIndex <= 5; archiveIndex++)
        {
            yield return $"{basePath}.{archiveIndex}";
        }
    }

    private static string SanitizeProfileId(string profileId)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(profileId.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
    }
}
