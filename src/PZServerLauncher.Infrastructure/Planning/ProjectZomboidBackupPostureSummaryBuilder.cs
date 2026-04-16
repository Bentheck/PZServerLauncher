using System.Globalization;
using System.Text.RegularExpressions;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Infrastructure.Planning;

public static class ProjectZomboidBackupPostureSummaryBuilder
{
    private static readonly Regex BackupPattern = new(
        "-(manual|preupdate|scheduled)-(\\d{8}-\\d{6})\\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ProjectZomboidBackupPostureSummary Build(
        ServerProfile profile,
        IReadOnlyList<string>? backups,
        string? selectedBackup,
        string runtimeState)
    {
        var parsedBackups = (backups ?? [])
            .Select(ParseBackup)
            .ToList();

        var manualCount = parsedBackups.Count(x => x.Trigger == BackupTrigger.Manual);
        var preUpdateCount = parsedBackups.Count(x => x.Trigger == BackupTrigger.PreUpdate);
        var scheduledCount = parsedBackups.Count(x => x.Trigger == BackupTrigger.Scheduled);
        var totalCount = parsedBackups.Count;

        var latest = parsedBackups.FirstOrDefault() ?? ParsedBackup.Empty;
        var selected = ParseBackup(selectedBackup);

        var coverageSummary = totalCount == 0
            ? "No recovery archives are available yet. Capture a manual snapshot before major config or update work."
            : $"{totalCount} recovery archive(s) available: {manualCount} manual, {preUpdateCount} pre-update, {scheduledCount} scheduled.";

        var latestArchiveSummary = latest.FileName is null
            ? "Latest archive: none captured yet."
            : $"Latest archive: {DescribeBackup(latest)}.";

        var selectedArchiveSummary = selected.FileName is null
            ? "No recovery point is currently selected."
            : $"Selected recovery point: {DescribeBackup(selected)}.";

        var retentionSummary = BuildRetentionSummary(profile.BackupPolicy);
        var restoreSafetySummary = BuildRestoreSafetySummary(totalCount > 0, runtimeState);
        var continuitySummary = BuildContinuitySummary(profile.BackupPolicy, manualCount, preUpdateCount, scheduledCount);
        var archiveMixSummary = totalCount == 0
            ? "Archive mix: no manual, pre-update, or scheduled history exists yet."
            : $"Archive mix: {manualCount} manual | {preUpdateCount} pre-update | {scheduledCount} scheduled.";

        return new ProjectZomboidBackupPostureSummary(
            coverageSummary,
            latestArchiveSummary,
            selectedArchiveSummary,
            retentionSummary,
            restoreSafetySummary,
            continuitySummary,
            archiveMixSummary,
            totalCount,
            manualCount,
            preUpdateCount,
            scheduledCount,
            manualCount > 0,
            preUpdateCount > 0,
            scheduledCount > 0);
    }

    private static ParsedBackup ParseBackup(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ParsedBackup.Empty;
        }

        var match = BackupPattern.Match(fileName);
        if (!match.Success)
        {
            return new ParsedBackup(fileName, null, null);
        }

        var trigger = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "manual" => BackupTrigger.Manual,
            "preupdate" => BackupTrigger.PreUpdate,
            "scheduled" => BackupTrigger.Scheduled,
            _ => (BackupTrigger?)null,
        };

        DateTimeOffset? createdAtUtc = DateTimeOffset.TryParseExact(
            match.Groups[2].Value,
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedTimestamp)
            ? parsedTimestamp
            : null;

        return new ParsedBackup(fileName, trigger, createdAtUtc);
    }

    private static string DescribeBackup(ParsedBackup backup)
    {
        if (backup.FileName is null)
        {
            return "none";
        }

        var pieces = new List<string> { backup.FileName };
        if (backup.Trigger is not null)
        {
            pieces.Add(backup.Trigger switch
            {
                BackupTrigger.Manual => "manual snapshot",
                BackupTrigger.PreUpdate => "pre-update safety net",
                BackupTrigger.Scheduled => "scheduled archive",
                _ => "archive",
            });
        }

        if (backup.CreatedAtUtc is not null)
        {
            pieces.Add(backup.CreatedAtUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        }

        return string.Join(" | ", pieces);
    }

    private static string BuildRetentionSummary(BackupPolicy policy)
    {
        var manualSummary = policy.KeepManualBackupsForever
            ? "manual backups are kept forever"
            : "manual backups follow retention";
        var preUpdateSummary = policy.PreUpdateBackupEnabled
            ? $"pre-update safety is set to keep the last {policy.PreUpdateBackupRetentionCount}"
            : "pre-update safety is disabled";
        var scheduledSummary = policy.ScheduledBackupsEnabled
            ? $"{ScheduledBackupPlanner.DescribeCadence(policy)} and keep the last {policy.ScheduledBackupRetentionCount}"
            : "scheduled snapshots are disabled";
        return $"Retention posture: {manualSummary}, {preUpdateSummary}, and {scheduledSummary}.";
    }

    private static string BuildRestoreSafetySummary(bool hasBackups, string runtimeState)
    {
        if (!hasBackups)
        {
            return "Restore is blocked until the first archive exists. Capture a manual backup before risky changes.";
        }

        return string.Equals(runtimeState, "Running", StringComparison.OrdinalIgnoreCase)
            ? "Restore will require a stop before files roll back. The host can restart the server afterward if requested."
            : "The server is idle, so restore can proceed immediately and optionally request a restart afterward.";
    }

    private static string BuildContinuitySummary(BackupPolicy policy, int manualCount, int preUpdateCount, int scheduledCount)
    {
        var gaps = new List<string>();

        if (manualCount == 0)
        {
            gaps.Add("manual recovery point missing");
        }

        if (policy.PreUpdateBackupEnabled && preUpdateCount == 0)
        {
            gaps.Add("no pre-update safety archive captured yet");
        }

        if (policy.ScheduledBackupsEnabled && scheduledCount == 0)
        {
            gaps.Add("scheduled snapshots enabled but history is still empty");
        }

        return gaps.Count == 0
            ? "Recovery continuity looks healthy across manual, pre-update, and scheduled coverage."
            : $"Recovery continuity gap: {string.Join("; ", gaps)}.";
    }

    private sealed record ParsedBackup(string? FileName, BackupTrigger? Trigger, DateTimeOffset? CreatedAtUtc)
    {
        public static ParsedBackup Empty { get; } = new(null, null, null);
    }
}
