using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Tests.Profiles;

public sealed class ScheduledBackupPlannerTests
{
    [Fact]
    public void TryGetDueScheduledRunUtc_ReturnsCurrentSlotWhenBackupIsMissing()
    {
        var policy = BackupPolicy.Default with
        {
            ScheduledBackupsEnabled = true,
            ScheduledBackupStartLocalTime = "03:00",
            ScheduledBackupIntervalHours = 6,
        };

        var nowUtc = new DateTimeOffset(2026, 4, 15, 15, 5, 0, TimeSpan.Zero);
        var lastScheduledBackupUtc = new DateTimeOffset(2026, 4, 15, 9, 0, 0, TimeSpan.Zero);

        var due = ScheduledBackupPlanner.TryGetDueScheduledRunUtc(
            policy,
            nowUtc,
            lastScheduledBackupUtc,
            out var dueRunUtc,
            TimeZoneInfo.Utc);

        Assert.True(due);
        Assert.Equal(new DateTimeOffset(2026, 4, 15, 15, 0, 0, TimeSpan.Zero), dueRunUtc);
    }

    [Fact]
    public void TryGetDueScheduledRunUtc_ReturnsFalseWhenCurrentSlotAlreadyHasBackup()
    {
        var policy = BackupPolicy.Default with
        {
            ScheduledBackupsEnabled = true,
            ScheduledBackupStartLocalTime = "03:00",
            ScheduledBackupIntervalHours = 6,
        };

        var nowUtc = new DateTimeOffset(2026, 4, 15, 15, 5, 0, TimeSpan.Zero);
        var lastScheduledBackupUtc = new DateTimeOffset(2026, 4, 15, 15, 0, 0, TimeSpan.Zero);

        var due = ScheduledBackupPlanner.TryGetDueScheduledRunUtc(
            policy,
            nowUtc,
            lastScheduledBackupUtc,
            out _,
            TimeZoneInfo.Utc);

        Assert.False(due);
    }

    [Fact]
    public void GetNextScheduledRunUtc_ReturnsNextCadenceBoundary()
    {
        var policy = BackupPolicy.Default with
        {
            ScheduledBackupsEnabled = true,
            ScheduledBackupStartLocalTime = "03:00",
            ScheduledBackupIntervalHours = 6,
        };

        var nowUtc = new DateTimeOffset(2026, 4, 15, 15, 5, 0, TimeSpan.Zero);

        var nextRunUtc = ScheduledBackupPlanner.GetNextScheduledRunUtc(policy, nowUtc, TimeZoneInfo.Utc);

        Assert.Equal(new DateTimeOffset(2026, 4, 15, 21, 0, 0, TimeSpan.Zero), nextRunUtc);
    }
}
