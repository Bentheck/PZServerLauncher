using System.Collections.Concurrent;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class RuntimeStateStore(
    ProjectZomboidLiveOperationsInterpreter liveOperationsInterpreter,
    IRuntimeLogSink? runtimeLogSink = null)
{
    private const int LogBufferLimit = 500;
    private const int PlayerSignalLimit = 50;
    private const int OperatorActionLimit = 50;
    private readonly ConcurrentDictionary<string, ServerRuntimeStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _logs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConnectedPlayerSession>> _connectedPlayers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<PlayerActivitySignal>> _playerSignals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<OperatorActionRecord>> _operatorActions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ServerRuntimeStatus> ListStatuses() => _statuses.Keys
        .OrderBy(profileId => profileId, StringComparer.OrdinalIgnoreCase)
        .Select(GetOrDefault)
        .ToList();

    public ServerRuntimeStatus GetOrDefault(string profileId) =>
        EnrichStatus(_statuses.TryGetValue(profileId, out var status)
            ? status
            : new ServerRuntimeStatus(profileId, ServerRuntimeState.Stopped, null, null, null, null, null));

    public void Update(ServerRuntimeStatus status)
    {
        _statuses[status.ProfileId] = status;
    }

    public ProfileLiveOperationsSnapshot? AppendLog(string profileId, string line)
    {
        runtimeLogSink?.WriteProfileLine(profileId, line);

        var queue = _logs.GetOrAdd(profileId, _ => new ConcurrentQueue<string>());
        queue.Enqueue(line);

        while (queue.Count > LogBufferLimit && queue.TryDequeue(out _))
        {
        }

        var current = GetOrDefault(profileId);
        Update(current with { LatestLogLine = line });

        var signal = liveOperationsInterpreter.TryParse(line, DateTimeOffset.UtcNow);
        if (signal is null)
        {
            return null;
        }

        ApplyPlayerSignal(profileId, signal);
        return GetLiveOperations(profileId);
    }

    public IReadOnlyList<string> GetRecentLogs(string profileId) =>
        _logs.TryGetValue(profileId, out var queue)
            ? queue.ToArray()
            : [];

    public ProfileLiveOperationsSnapshot GetLiveOperations(string profileId)
    {
        var players = _connectedPlayers.TryGetValue(profileId, out var roster)
            ? roster.Values.OrderBy(player => player.UserName, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        var recentSignals = _playerSignals.TryGetValue(profileId, out var signals)
            ? signals.ToArray().OrderByDescending(signal => signal.TimestampUtc).ToList()
            : [];
        var operatorActions = _operatorActions.TryGetValue(profileId, out var actions)
            ? actions.ToArray().OrderByDescending(action => action.TimestampUtc).ToList()
            : [];
        var lastPlayerActivityAtUtc = recentSignals.Count == 0
            ? (DateTimeOffset?)null
            : recentSignals.Max(signal => signal.TimestampUtc);

        return new ProfileLiveOperationsSnapshot(
            profileId,
            players,
            recentSignals,
            operatorActions,
            true,
            lastPlayerActivityAtUtc);
    }

    public ProfileLiveOperationsSnapshot RecordOperatorAction(string profileId, string kind, string commandText, string summary)
    {
        var queue = _operatorActions.GetOrAdd(profileId, _ => new ConcurrentQueue<OperatorActionRecord>());
        var recordedAtUtc = DateTimeOffset.UtcNow;
        queue.Enqueue(new OperatorActionRecord(kind, commandText, summary, recordedAtUtc));
        TrimQueue(queue, OperatorActionLimit);

        var current = GetOrDefault(profileId);
        Update(current with
        {
            LastOperatorCommandSummary = summary,
        });
        return GetLiveOperations(profileId);
    }

    public ProfileLiveOperationsSnapshot ResetLiveOperations(string profileId)
    {
        _connectedPlayers.TryRemove(profileId, out _);
        _playerSignals.TryRemove(profileId, out _);
        var current = GetOrDefault(profileId);
        Update(current with { ConnectedPlayerCount = 0, LastPlayerActivityAtUtc = null });
        return GetLiveOperations(profileId);
    }

    public void ClearProfile(string profileId)
    {
        _statuses.TryRemove(profileId, out _);
        _logs.TryRemove(profileId, out _);
        _connectedPlayers.TryRemove(profileId, out _);
        _playerSignals.TryRemove(profileId, out _);
        _operatorActions.TryRemove(profileId, out _);
    }

    private void ApplyPlayerSignal(string profileId, PlayerActivitySignal signal)
    {
        var roster = _connectedPlayers.GetOrAdd(profileId, _ => new ConcurrentDictionary<string, ConnectedPlayerSession>(StringComparer.OrdinalIgnoreCase));
        if (string.Equals(signal.Activity, "Joined", StringComparison.OrdinalIgnoreCase))
        {
            roster[signal.UserName] = new ConnectedPlayerSession(signal.UserName, signal.TimestampUtc, signal.TimestampUtc);
        }
        else
        {
            roster.TryRemove(signal.UserName, out _);
        }

        var queue = _playerSignals.GetOrAdd(profileId, _ => new ConcurrentQueue<PlayerActivitySignal>());
        queue.Enqueue(signal);
        TrimQueue(queue, PlayerSignalLimit);

        var current = GetOrDefault(profileId);
        Update(current with
        {
            ConnectedPlayerCount = roster.Count,
            LastPlayerActivityAtUtc = signal.TimestampUtc,
        });
    }

    private ServerRuntimeStatus EnrichStatus(ServerRuntimeStatus status)
    {
        var playerCount = _connectedPlayers.TryGetValue(status.ProfileId, out var roster) ? roster.Count : 0;
        var lastPlayerActivityAtUtc = _playerSignals.TryGetValue(status.ProfileId, out var signals) && signals.Count > 0
            ? signals.Max(signal => signal.TimestampUtc)
            : (DateTimeOffset?)null;
        var lastOperatorSummary = _operatorActions.TryGetValue(status.ProfileId, out var actions)
            ? actions.ToArray().OrderByDescending(action => action.TimestampUtc).FirstOrDefault()?.Summary
            : status.LastOperatorCommandSummary;

        return status with
        {
            ConnectedPlayerCount = playerCount,
            LastPlayerActivityAtUtc = lastPlayerActivityAtUtc,
            LastOperatorCommandSummary = lastOperatorSummary,
        };
    }

    private static void TrimQueue<T>(ConcurrentQueue<T> queue, int limit)
    {
        while (queue.Count > limit && queue.TryDequeue(out _))
        {
        }
    }
}
