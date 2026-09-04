namespace PZServerLauncher.Core.Runtime;

public sealed class LogDisplayCompactor
{
    private string? _activeWarningKey;
    private string? _firstWarningLine;
    private int _warningCount;

    public LogDisplayCompaction Append(string line)
    {
        var warningKey = GetWarningKey(line);
        if (warningKey is null)
        {
            Reset();
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

        _activeWarningKey = warningKey;
        _firstWarningLine = line;
        _warningCount = 1;
        return new LogDisplayCompaction(false, line);
    }

    public void Reset()
    {
        _activeWarningKey = null;
        _firstWarningLine = null;
        _warningCount = 0;
    }

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

public sealed record LogDisplayCompaction(bool ReplacePrevious, string DisplayLine);
