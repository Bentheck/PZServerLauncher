using System.Globalization;

namespace PZServerLauncher.Core.Profiles;

public static class ScheduledBackupPlanner
{
    private static readonly string[] LocalTimeFormats = ["HH:mm", "H:mm"];

    public static bool IsValidIntervalHours(int hours) =>
        hours is >= 1 and <= 168;

    public static bool TryParseStartLocalTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(
            value,
            LocalTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);

    public static string DescribeCadence(BackupPolicy policy)
    {
        if (!TryParseStartLocalTime(policy.ScheduledBackupStartLocalTime, out var startTime) ||
            !IsValidIntervalHours(policy.ScheduledBackupIntervalHours))
        {
            return "scheduled snapshots have an invalid cadence";
        }

        var hourLabel = policy.ScheduledBackupIntervalHours == 1 ? "hour" : "hours";
        return $"scheduled snapshots start at {startTime:HH\\:mm} local and repeat every {policy.ScheduledBackupIntervalHours} {hourLabel}";
    }

    public static DateTimeOffset? GetNextScheduledRunUtc(
        BackupPolicy policy,
        DateTimeOffset nowUtc,
        TimeZoneInfo? timeZone = null)
    {
        if (!TryBuildSchedule(policy, nowUtc, timeZone, out var schedule))
        {
            return null;
        }

        var currentSlotLocal = GetCurrentSlotLocal(schedule.NowLocal, schedule.StartTime, schedule.Interval);
        var nextSlotLocal = currentSlotLocal <= schedule.NowLocal
            ? currentSlotLocal.Add(schedule.Interval)
            : currentSlotLocal;
        return ConvertLocalToUtc(nextSlotLocal, schedule.TimeZone);
    }

    public static bool TryGetDueScheduledRunUtc(
        BackupPolicy policy,
        DateTimeOffset nowUtc,
        DateTimeOffset? lastScheduledBackupUtc,
        out DateTimeOffset dueRunUtc,
        TimeZoneInfo? timeZone = null)
    {
        dueRunUtc = default;
        if (!policy.ScheduledBackupsEnabled ||
            !TryBuildSchedule(policy, nowUtc, timeZone, out var schedule))
        {
            return false;
        }

        var currentSlotLocal = GetCurrentSlotLocal(schedule.NowLocal, schedule.StartTime, schedule.Interval);
        var currentSlotUtc = ConvertLocalToUtc(currentSlotLocal, schedule.TimeZone);
        if (schedule.NowUtc < currentSlotUtc)
        {
            return false;
        }

        if (lastScheduledBackupUtc is not null && lastScheduledBackupUtc.Value >= currentSlotUtc)
        {
            return false;
        }

        dueRunUtc = currentSlotUtc;
        return true;
    }

    private static bool TryBuildSchedule(
        BackupPolicy policy,
        DateTimeOffset nowUtc,
        TimeZoneInfo? timeZone,
        out ScheduleContext schedule)
    {
        schedule = default;
        if (!TryParseStartLocalTime(policy.ScheduledBackupStartLocalTime, out var startTime) ||
            !IsValidIntervalHours(policy.ScheduledBackupIntervalHours))
        {
            return false;
        }

        var resolvedTimeZone = timeZone ?? TimeZoneInfo.Local;
        schedule = new ScheduleContext(
            nowUtc,
            TimeZoneInfo.ConvertTime(nowUtc, resolvedTimeZone).DateTime,
            startTime,
            TimeSpan.FromHours(policy.ScheduledBackupIntervalHours),
            resolvedTimeZone);
        return true;
    }

    private static DateTime GetCurrentSlotLocal(DateTime nowLocal, TimeOnly startTime, TimeSpan interval)
    {
        var anchorLocal = new DateTime(2000, 1, 1, startTime.Hour, startTime.Minute, 0, DateTimeKind.Unspecified);
        var elapsed = nowLocal - anchorLocal;
        if (elapsed <= TimeSpan.Zero)
        {
            return anchorLocal;
        }

        var slotIndex = elapsed.Ticks / interval.Ticks;
        return anchorLocal.AddTicks(slotIndex * interval.Ticks);
    }

    private static DateTimeOffset ConvertLocalToUtc(DateTime localDateTime, TimeZoneInfo timeZone)
    {
        var adjustedLocalDateTime = localDateTime;
        while (timeZone.IsInvalidTime(adjustedLocalDateTime))
        {
            adjustedLocalDateTime = adjustedLocalDateTime.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(adjustedLocalDateTime))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(adjustedLocalDateTime);
            return new DateTimeOffset(adjustedLocalDateTime, offsets.Max()).ToUniversalTime();
        }

        return new DateTimeOffset(adjustedLocalDateTime, timeZone.GetUtcOffset(adjustedLocalDateTime)).ToUniversalTime();
    }

    private readonly record struct ScheduleContext(
        DateTimeOffset NowUtc,
        DateTime NowLocal,
        TimeOnly StartTime,
        TimeSpan Interval,
        TimeZoneInfo TimeZone);
}
