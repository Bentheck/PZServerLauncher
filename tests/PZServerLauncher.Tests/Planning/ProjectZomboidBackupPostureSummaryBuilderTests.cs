using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Infrastructure.Planning;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Planning;

public sealed class ProjectZomboidBackupPostureSummaryBuilderTests
{
    [Fact]
    public void Build_ReportsCoverageMixAndRetentionFromBackupHistory()
    {
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "servertest",
            BackupPolicy = BackupPolicy.Default with
            {
                ScheduledBackupsEnabled = true,
                ScheduledBackupRetentionCount = 8,
                ScheduledBackupIntervalHours = 6,
                ScheduledBackupStartLocalTime = "03:00",
                PreUpdateBackupRetentionCount = 4,
            },
        };

        var backups = new[]
        {
            "servertest-manual-20260408-150000.zip",
            "servertest-preupdate-20260408-140000.zip",
            "servertest-scheduled-20260408-130000.zip",
        };

        var summary = ProjectZomboidBackupPostureSummaryBuilder.Build(
            profile,
            backups,
            backups[1],
            runtimeState: "Running");

        Assert.Equal(3, summary.TotalBackupCount);
        Assert.Equal(1, summary.ManualBackupCount);
        Assert.Equal(1, summary.PreUpdateBackupCount);
        Assert.Equal(1, summary.ScheduledBackupCount);
        Assert.Contains("3 recovery archive(s) available", summary.CoverageSummary);
        Assert.Contains("manual snapshot", summary.LatestArchiveSummary);
        Assert.Contains("pre-update safety net", summary.SelectedArchiveSummary);
        Assert.Contains("keep the last 4", summary.RetentionSummary);
        Assert.Contains("keep the last 8", summary.RetentionSummary);
        Assert.Contains("start at 03:00 local and repeat every 6 hours", summary.RetentionSummary);
        Assert.Contains("require a stop", summary.RestoreSafetySummary);
        Assert.Contains("looks healthy", summary.ContinuitySummary);
        Assert.Contains("1 manual | 1 pre-update | 1 scheduled", summary.ArchiveMixSummary);
    }

    [Fact]
    public void Build_ReportsContinuityGapsWhenCoverageIsMissing()
    {
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "servertest",
            BackupPolicy = BackupPolicy.Default with
            {
                ScheduledBackupsEnabled = true,
            },
        };

        var summary = ProjectZomboidBackupPostureSummaryBuilder.Build(
            profile,
            [],
            selectedBackup: null,
            runtimeState: "Stopped");

        Assert.Equal(0, summary.TotalBackupCount);
        Assert.Contains("No recovery archives are available yet", summary.CoverageSummary);
        Assert.Contains("none captured yet", summary.LatestArchiveSummary);
        Assert.Contains("blocked until the first archive exists", summary.RestoreSafetySummary);
        Assert.Contains("manual recovery point missing", summary.ContinuitySummary);
        Assert.Contains("no pre-update safety archive captured yet", summary.ContinuitySummary);
        Assert.Contains("scheduled snapshots enabled but history is still empty", summary.ContinuitySummary);
    }
}
