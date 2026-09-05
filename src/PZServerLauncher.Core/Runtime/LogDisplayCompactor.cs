using System.Globalization;

namespace PZServerLauncher.Core.Runtime;

public sealed class LogDisplayCompactor
{
    private string? _activeWarningKey;
    private string? _firstWarningLine;
    private int _warningCount;
    private readonly HashSet<string> _seenNoisyWarningKeys = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeWorkshopDownloadId;
    private int _workshopDownloadLineCount;
    private bool _activeWorkshopHasProgress;
    private int _activeWorkshopProgressBucket = -1;
    private bool _hasDisplayedModOverrideNotice;

    public LogDisplayCompaction Append(string line)
    {
        if (IsDiscardedNoise(line))
        {
            return new LogDisplayCompaction(false, string.Empty, Suppress: true);
        }

        var workshopDownload = ParseWorkshopDownload(line);
        if (workshopDownload is not null)
        {
            ResetWarningGroup();
            var replacePrevious = string.Equals(
                _activeWorkshopDownloadId,
                workshopDownload.WorkshopId,
                StringComparison.OrdinalIgnoreCase);
            _workshopDownloadLineCount = replacePrevious ? _workshopDownloadLineCount + 1 : 1;
            var progressBucket = GetProgressBucket(workshopDownload);
            var shouldDisplay = !replacePrevious ||
                                (progressBucket is not null &&
                                 (!_activeWorkshopHasProgress || progressBucket > _activeWorkshopProgressBucket));
            _activeWorkshopDownloadId = workshopDownload.WorkshopId;
            if (progressBucket is not null)
            {
                _activeWorkshopHasProgress = true;
                _activeWorkshopProgressBucket = Math.Max(_activeWorkshopProgressBucket, progressBucket.Value);
            }

            if (!shouldDisplay)
            {
                return new LogDisplayCompaction(false, string.Empty, Suppress: true);
            }

            return new LogDisplayCompaction(
                replacePrevious,
                FormatWorkshopDownload(workshopDownload, _workshopDownloadLineCount));
        }

        ResetWorkshopDownloadGroup();
        if (IsModOverride(line))
        {
            ResetWarningGroup();
            if (_hasDisplayedModOverrideNotice)
            {
                return new LogDisplayCompaction(false, string.Empty, Suppress: true);
            }

            _hasDisplayedModOverrideNotice = true;
            return new LogDisplayCompaction(false, "Mods overrides in progress…");
        }

        var warningKey = GetWarningKey(line);
        if (warningKey is null)
        {
            ResetWarningGroup();
            return new LogDisplayCompaction(false, line);
        }

        if (string.Equals(_activeWarningKey, warningKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(_firstWarningLine))
        {
            _warningCount += 1;
            return new LogDisplayCompaction(
                true,
                $"{_firstWarningLine} [{_warningCount - 1} similar warning{(_warningCount == 2 ? string.Empty : "s")} hidden]");
        }

        // Known asset-warning floods often contain unrelated log lines or alternate
        // between warning families. Remember them for the whole buffer so those
        // interruptions cannot restart thousands of identical UI entries.
        if (IsNoisyWarningKey(warningKey) && !_seenNoisyWarningKeys.Add(warningKey))
        {
            ResetWarningGroup();
            return new LogDisplayCompaction(false, string.Empty, Suppress: true);
        }

        _activeWarningKey = warningKey;
        _firstWarningLine = line;
        _warningCount = 1;
        return new LogDisplayCompaction(false, line);
    }

    public void Reset()
    {
        ResetWarningGroup();
        ResetWorkshopDownloadGroup();
        _seenNoisyWarningKeys.Clear();
        _hasDisplayedModOverrideNotice = false;
    }

    public static bool IsDiscardedNoise(string line) =>
        line.Contains("Could not find icon:", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("no such mesh", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("could not find bone index", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("cannot find bone index", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("sun.nio", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("java.nio", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("zombie.core", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("ERROR: General", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Stack trace:", StringComparison.OrdinalIgnoreCase);

    private void ResetWarningGroup()
    {
        _activeWarningKey = null;
        _firstWarningLine = null;
        _warningCount = 0;
    }

    private void ResetWorkshopDownloadGroup()
    {
        _activeWorkshopDownloadId = null;
        _workshopDownloadLineCount = 0;
        _activeWorkshopHasProgress = false;
        _activeWorkshopProgressBucket = -1;
    }

    private static bool IsModOverride(string line)
    {
        var payload = ExtractPayload(line);
        const string prefix = "mod \"";
        const string separator = "\" overrides ";
        if (!payload.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separatorIndex = payload.IndexOf(separator, prefix.Length, StringComparison.OrdinalIgnoreCase);
        return separatorIndex >= 0 && separatorIndex + separator.Length < payload.Length;
    }

    private static WorkshopDownloadLine? ParseWorkshopDownload(string line)
    {
        if (!line.Contains("Workshop:", StringComparison.OrdinalIgnoreCase) ||
            (!line.Contains("DownloadPending", StringComparison.OrdinalIgnoreCase) &&
             !line.Contains("Workshop: download ", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var idMarker = line.LastIndexOf("ID=", StringComparison.OrdinalIgnoreCase);
        if (idMarker < 0)
        {
            return null;
        }

        var workshopId = new string(line[(idMarker + 3)..].TakeWhile(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(workshopId))
        {
            return null;
        }

        var progressMarker = line.IndexOf("Workshop: download ", StringComparison.OrdinalIgnoreCase);
        if (progressMarker < 0)
        {
            return new WorkshopDownloadLine(workshopId, null, null);
        }

        var progress = line[(progressMarker + "Workshop: download ".Length)..idMarker].Trim();
        var separator = progress.IndexOf('/');
        return separator > 0 &&
               long.TryParse(progress[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var downloadedBytes) &&
               long.TryParse(progress[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var totalBytes)
            ? new WorkshopDownloadLine(workshopId, downloadedBytes, totalBytes)
            : new WorkshopDownloadLine(workshopId, null, null);
    }

    private static string FormatWorkshopDownload(WorkshopDownloadLine download, int lineCount)
    {
        var progress = download.TotalBytes > 0
            ? $"{(download.DownloadedBytes.GetValueOrDefault() * 100d / download.TotalBytes.Value).ToString("0.0", CultureInfo.InvariantCulture)}% downloaded"
            : "download pending";
        var compacted = lineCount > 1
            ? $" [{lineCount - 1} repetitive update{(lineCount == 2 ? string.Empty : "s")} compacted]"
            : string.Empty;
        return $"Workshop item {download.WorkshopId}: {progress}{compacted}";
    }

    private static int? GetProgressBucket(WorkshopDownloadLine download) =>
        download.TotalBytes > 0
            ? (int)Math.Clamp(
                download.DownloadedBytes.GetValueOrDefault() * 20 / download.TotalBytes.Value,
                0,
                20)
            : null;

    private static string? GetWarningKey(string line)
    {
        if (!line.Contains("warn", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = ExtractPayload(line);

        if (payload.Contains("duplicate texture", StringComparison.OrdinalIgnoreCase))
        {
            return "asset:duplicate-texture";
        }

        if (payload.Contains("Could not find icon:", StringComparison.OrdinalIgnoreCase))
        {
            return "asset:missing-icon";
        }

        if (payload.Contains("extents != physicsChassisShape", StringComparison.OrdinalIgnoreCase))
        {
            return "asset:vehicle-physics-extents";
        }

        return $"warning:{payload}";
    }

    private static bool IsNoisyWarningKey(string warningKey) =>
        warningKey.StartsWith("asset:", StringComparison.OrdinalIgnoreCase);

    private static string ExtractPayload(string line)
    {
        var messageSeparator = line.IndexOf('>');
        if (messageSeparator >= 0 && messageSeparator + 1 < line.Length)
        {
            return line[(messageSeparator + 1)..].Trim();
        }

        var timestampSeparator = line.IndexOf(' ');
        return timestampSeparator >= 0 && timestampSeparator + 1 < line.Length
            ? line[(timestampSeparator + 1)..].Trim()
            : line.Trim();
    }
}

public sealed record LogDisplayCompaction(bool ReplacePrevious, string DisplayLine, bool Suppress = false);

internal sealed record WorkshopDownloadLine(string WorkshopId, long? DownloadedBytes, long? TotalBytes);
