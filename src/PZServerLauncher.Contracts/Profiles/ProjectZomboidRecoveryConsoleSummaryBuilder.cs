using PZServerLauncher.Core.Planning;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidRecoveryConsoleSummary(
    string CoverageHeadline,
    string LatestKnownGoodHeadline,
    string RestoreRiskHeadline,
    string RetentionHeadline,
    string OperatorSummary,
    string SelectionSummary,
    IReadOnlyList<string> Checklist);

public static class ProjectZomboidRecoveryConsoleSummaryBuilder
{
    public static ProjectZomboidRecoveryConsoleSummary Build(
        ProjectZomboidBackupPostureSummary posture,
        string runtimeState,
        bool installDetected,
        bool cacheDetected,
        bool restartAfterRestore,
        string? selectedArchiveName,
        bool selectedArchiveIsLatest)
    {
        var hasBackups = posture.TotalBackupCount > 0;
        var running = string.Equals(runtimeState, "Running", StringComparison.OrdinalIgnoreCase);

        var coverageHeadline = !hasBackups
            ? "No recovery point"
            : posture.HasManualBackups && posture.HasPreUpdateBackups
                ? "Layered coverage"
                : posture.HasManualBackups
                    ? "Manual-first coverage"
                    : posture.HasScheduledBackups || posture.HasPreUpdateBackups
                        ? "Automated-only coverage"
                        : "Recovery coverage available";

        var latestKnownGoodHeadline = !hasBackups
            ? "No known-good archive"
            : string.IsNullOrWhiteSpace(selectedArchiveName)
                ? "Latest known-good selected"
                : selectedArchiveIsLatest
                    ? "Latest known-good selected"
                    : "Alternate rollback point selected";

        var restoreRiskHeadline = !hasBackups
            ? "Restore is blocked until the first archive exists."
            : !installDetected || !cacheDetected
                ? "Restore risk is elevated because the install footprint is incomplete."
                : running
                    ? restartAfterRestore
                        ? "Live restore will stop the server, unpack the archive, then request a restart."
                        : "Live restore will stop the server and leave it offline for inspection."
                    : restartAfterRestore
                        ? "Idle restore can proceed and request a restart afterward."
                        : "Idle restore can proceed and leave the world offline for inspection.";

        var retentionHeadline = posture.HasManualBackups && posture.HasPreUpdateBackups && posture.HasScheduledBackups
            ? "Manual, pre-update, and scheduled history are all represented."
            : posture.HasManualBackups && posture.HasPreUpdateBackups
                ? "Manual and pre-update safety nets are present."
                : posture.HasManualBackups
                    ? "Manual archives are carrying recovery posture."
                    : posture.TotalBackupCount > 0
                        ? "Retention is leaning on automated history only."
                        : "Retention posture has not started yet.";

        var selectionSummary = !hasBackups
            ? "No archive is selected because none have been captured yet."
            : string.IsNullOrWhiteSpace(selectedArchiveName)
                ? "The latest archive is acting as the default recovery point."
                : selectedArchiveIsLatest
                    ? $"The selected archive is the latest known-good point: {selectedArchiveName}."
                    : $"The selected archive is an older rollback point: {selectedArchiveName}.";

        var operatorSummary = BuildOperatorSummary(
            hasBackups,
            installDetected,
            cacheDetected,
            running,
            restartAfterRestore,
            posture,
            selectedArchiveIsLatest);

        return new ProjectZomboidRecoveryConsoleSummary(
            coverageHeadline,
            latestKnownGoodHeadline,
            restoreRiskHeadline,
            retentionHeadline,
            operatorSummary,
            selectionSummary,
            BuildChecklist(
                hasBackups,
                installDetected,
                cacheDetected,
                running,
                restartAfterRestore,
                posture,
                selectedArchiveIsLatest));
    }

    public static ProjectZomboidRecoveryConsoleSummary Empty() =>
        Build(
            new ProjectZomboidBackupPostureSummary(
                "Recovery posture unavailable.",
                "Latest archive unavailable.",
                "Selection unavailable.",
                "Retention unavailable.",
                "Restore safety unavailable.",
                "Continuity unavailable.",
                "Archive mix unavailable.",
                0,
                0,
                0,
                0,
                false,
                false,
                false),
            runtimeState: "Stopped",
            installDetected: false,
            cacheDetected: false,
            restartAfterRestore: true,
            selectedArchiveName: null,
            selectedArchiveIsLatest: false);

    private static string BuildOperatorSummary(
        bool hasBackups,
        bool installDetected,
        bool cacheDetected,
        bool running,
        bool restartAfterRestore,
        ProjectZomboidBackupPostureSummary posture,
        bool selectedArchiveIsLatest)
    {
        if (!hasBackups)
        {
            return "Create the first manual backup now. Until then, every risky change is happening without a known-good rollback point.";
        }

        if (!installDetected || !cacheDetected)
        {
            return "The archive history exists, but the install or cache footprint is incomplete. Confirm the server layout before trusting a restore.";
        }

        if (!selectedArchiveIsLatest && !string.IsNullOrWhiteSpace(posture.SelectedArchiveSummary))
        {
            return "You are targeting an older rollback point. Double-check that this is the exact state you want before restoring over newer data.";
        }

        if (running)
        {
            return restartAfterRestore
                ? "This restore will interrupt a live server and bring it back automatically. Warn players first and keep Logs open during recovery."
                : "This restore will interrupt a live server and leave it offline. That is the safer choice when you want to inspect files before re-entry.";
        }

        return posture.HasManualBackups
            ? "Recovery posture looks healthy. Use the selected archive deliberately, then verify Overview and Logs after restore."
            : "Recovery is available, but manual rollback coverage is still thin. Capture a known-good manual archive after this session.";
    }

    private static IReadOnlyList<string> BuildChecklist(
        bool hasBackups,
        bool installDetected,
        bool cacheDetected,
        bool running,
        bool restartAfterRestore,
        ProjectZomboidBackupPostureSummary posture,
        bool selectedArchiveIsLatest)
    {
        var checklist = new List<string>();

        if (!hasBackups)
        {
            checklist.Add("Create a manual backup before any update, mod, or cleanup work.");
            return checklist;
        }

        checklist.Add(selectedArchiveIsLatest
            ? "Confirm the latest known-good archive is really the point you want to roll back to."
            : "Double-check that the selected older archive is the intended rollback target.");

        if (running)
        {
            checklist.Add("Expect the host to stop the live server before recovery begins.");
        }

        checklist.Add(restartAfterRestore
            ? "Keep restart-after-restore enabled if you want the world to come back online automatically."
            : "Leave restart-after-restore disabled if you want to inspect files before the next launch.");

        if (!installDetected || !cacheDetected)
        {
            checklist.Add("Verify the install and cache paths before trusting the restored world state.");
        }

        if (!posture.HasManualBackups)
        {
            checklist.Add("Capture a fresh manual archive after recovery so you keep a clean rollback point.");
        }

        if (!posture.HasPreUpdateBackups)
        {
            checklist.Add("Future updates should create a pre-update safety net before patching the install.");
        }

        if (checklist.Count == 0)
        {
            checklist.Add("Review Overview and Logs after restore to confirm the world, runtime, and latest archive posture all line up.");
        }

        return checklist;
    }
}
