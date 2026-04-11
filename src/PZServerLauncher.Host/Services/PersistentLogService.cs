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

    private RollingFileLogWriter GetProfileWriter(string profileId)
    {
        lock (_profileWritersGate)
        {
            if (_profileWriters.TryGetValue(profileId, out var writer))
            {
                return writer;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeProfileId = string.Concat(profileId.Select(ch => invalidChars.Contains(ch) ? '_' : ch));
            writer = new RollingFileLogWriter(Path.Combine(appPaths.LogsDirectory, "profiles", $"{safeProfileId}.log"));
            _profileWriters[profileId] = writer;
            return writer;
        }
    }
}
