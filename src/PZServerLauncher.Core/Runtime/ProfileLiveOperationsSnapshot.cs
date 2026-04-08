namespace PZServerLauncher.Core.Runtime;

public sealed record ProfileLiveOperationsSnapshot(
    string ProfileId,
    IReadOnlyList<ConnectedPlayerSession> ConnectedPlayers,
    IReadOnlyList<PlayerActivitySignal> RecentPlayerSignals,
    IReadOnlyList<OperatorActionRecord> RecentOperatorActions,
    bool IsRosterInferredFromLogs,
    DateTimeOffset? LastPlayerActivityAtUtc);
