using System.Collections.Concurrent;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class RuntimeStateStore
{
    private readonly ConcurrentDictionary<string, ServerRuntimeStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _logs = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ServerRuntimeStatus> ListStatuses() => _statuses.Values.OrderBy(x => x.ProfileId).ToList();

    public ServerRuntimeStatus GetOrDefault(string profileId) =>
        _statuses.TryGetValue(profileId, out var status)
            ? status
            : new ServerRuntimeStatus(profileId, ServerRuntimeState.Stopped, null, null, null, null, null);

    public void Update(ServerRuntimeStatus status)
    {
        _statuses[status.ProfileId] = status;
    }

    public void AppendLog(string profileId, string line)
    {
        var queue = _logs.GetOrAdd(profileId, _ => new ConcurrentQueue<string>());
        queue.Enqueue(line);

        while (queue.Count > 250 && queue.TryDequeue(out _))
        {
        }

        var current = GetOrDefault(profileId);
        Update(current with { LatestLogLine = line });
    }

    public IReadOnlyList<string> GetRecentLogs(string profileId) =>
        _logs.TryGetValue(profileId, out var queue)
            ? queue.ToArray()
            : [];
}
