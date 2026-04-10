namespace PZServerLauncher.Core.Planning;

public sealed record ProjectZomboidBackupPostureSummary(
    string CoverageSummary,
    string LatestArchiveSummary,
    string SelectedArchiveSummary,
    string RetentionSummary,
    string RestoreSafetySummary,
    string ContinuitySummary,
    string ArchiveMixSummary,
    int TotalBackupCount,
    int ManualBackupCount,
    int PreUpdateBackupCount,
    int ScheduledBackupCount,
    bool HasManualBackups,
    bool HasPreUpdateBackups,
    bool HasScheduledBackups);
