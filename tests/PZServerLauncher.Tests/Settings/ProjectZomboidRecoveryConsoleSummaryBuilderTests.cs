using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Planning;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidRecoveryConsoleSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesLayeredCoverageAndLiveRestoreRisk()
    {
        var posture = new ProjectZomboidBackupPostureSummary(
            "3 recovery archive(s) available.",
            "Latest archive: manual-latest.zip.",
            "Selected recovery point: manual-latest.zip.",
            "Retention posture is healthy.",
            "Restore will require a stop before files roll back.",
            "Recovery continuity looks healthy.",
            "Archive mix: 1 manual | 1 pre-update | 1 scheduled.",
            3,
            1,
            1,
            1,
            true,
            true,
            true);

        var summary = ProjectZomboidRecoveryConsoleSummaryBuilder.Build(
            posture,
            runtimeState: "Running",
            installDetected: true,
            cacheDetected: true,
            restartAfterRestore: true,
            selectedArchiveName: "manual-latest.zip",
            selectedArchiveIsLatest: true);

        Assert.Equal("Layered coverage", summary.CoverageHeadline);
        Assert.Equal("Latest known-good selected", summary.LatestKnownGoodHeadline);
        Assert.Contains("request a restart", summary.RestoreRiskHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interrupt a live server", summary.OperatorSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.Contains("host to stop the live server", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_BlocksWhenNoBackupsExist()
    {
        var posture = new ProjectZomboidBackupPostureSummary(
            "No recovery archives are available yet.",
            "Latest archive: none.",
            "No recovery point is selected.",
            "Retention posture is unavailable.",
            "Restore is blocked.",
            "Recovery continuity gap: manual recovery point missing.",
            "Archive mix: none.",
            0,
            0,
            0,
            0,
            false,
            false,
            false);

        var summary = ProjectZomboidRecoveryConsoleSummaryBuilder.Build(
            posture,
            runtimeState: "Stopped",
            installDetected: false,
            cacheDetected: false,
            restartAfterRestore: false,
            selectedArchiveName: null,
            selectedArchiveIsLatest: false);

        Assert.Equal("No recovery point", summary.CoverageHeadline);
        Assert.Contains("blocked", summary.RestoreRiskHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Single(summary.Checklist);
        Assert.Contains("manual backup", summary.Checklist[0], StringComparison.OrdinalIgnoreCase);
    }
}
