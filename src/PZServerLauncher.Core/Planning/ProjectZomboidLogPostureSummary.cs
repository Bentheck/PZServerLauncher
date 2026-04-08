namespace PZServerLauncher.Core.Planning;

public sealed record ProjectZomboidLogPostureSummary(
    string BufferSummary,
    string LatestSignalSummary,
    string SignalPostureSummary,
    string OperatorFocusSummary,
    string RuntimeWindowSummary,
    string PlayerActivitySummary,
    string OperatorCommandSummary,
    int BufferedLineCount,
    int ErrorSignalCount,
    int WarningSignalCount,
    int ModSignalCount,
    int ConnectedPlayerCount,
    int OperatorCommandCount,
    bool HasErrorSignals,
    bool HasWarningSignals,
    bool HasModSignals,
    IReadOnlyList<string> ConnectedPlayers,
    IReadOnlyList<string> RecentPlayerSignals);
