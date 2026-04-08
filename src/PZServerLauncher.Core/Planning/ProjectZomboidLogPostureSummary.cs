namespace PZServerLauncher.Core.Planning;

public sealed record ProjectZomboidLogPostureSummary(
    string BufferSummary,
    string LatestSignalSummary,
    string SignalPostureSummary,
    string OperatorFocusSummary,
    string RuntimeWindowSummary,
    int BufferedLineCount,
    int ErrorSignalCount,
    int WarningSignalCount,
    int ModSignalCount,
    bool HasErrorSignals,
    bool HasWarningSignals,
    bool HasModSignals);
