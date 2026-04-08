using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Infrastructure.Planning;

public static class ProjectZomboidLogPostureSummaryBuilder
{
    public static ProjectZomboidLogPostureSummary Build(ServerRuntimeStatus? status, IReadOnlyList<string>? logLines)
    {
        var lines = (logLines ?? []).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var latestLine = status?.LatestLogLine ?? lines.LastOrDefault();
        var errorCount = lines.Count(IsErrorSignal);
        var warningCount = lines.Count(IsWarningSignal);
        var modCount = lines.Count(IsModSignal);

        var bufferSummary = lines.Count == 0
            ? "No buffered lines are available yet. Start the server or wait for the next runtime event."
            : $"Rolling runtime buffer currently holds {lines.Count} line(s).";

        var latestSignalSummary = string.IsNullOrWhiteSpace(latestLine)
            ? "Latest signal: no runtime output captured yet."
            : $"Latest signal: {latestLine}";

        var signalPostureSummary = errorCount > 0
            ? $"Recent buffer contains {errorCount} error or failure signal(s)."
            : warningCount > 0
                ? $"Recent buffer contains {warningCount} warning signal(s) that may need operator attention."
                : lines.Count > 0
                    ? "Recent buffer is active without obvious warning or error signals."
                    : "No runtime signals are buffered yet.";

        var operatorFocusSummary = BuildOperatorFocusSummary(status?.State, lines.Count, errorCount, modCount);
        var runtimeWindowSummary = BuildRuntimeWindowSummary(status);

        return new ProjectZomboidLogPostureSummary(
            bufferSummary,
            latestSignalSummary,
            signalPostureSummary,
            operatorFocusSummary,
            runtimeWindowSummary,
            lines.Count,
            errorCount,
            warningCount,
            modCount,
            errorCount > 0,
            warningCount > 0,
            modCount > 0);
    }

    private static string BuildOperatorFocusSummary(ServerRuntimeState? state, int lineCount, int errorCount, int modCount)
    {
        if (state == ServerRuntimeState.Running)
        {
            if (errorCount > 0)
            {
                return "The server is live, but recent output contains failure signals. Investigate those lines before the next restart or player join test.";
            }

            if (modCount > 0)
            {
                return "Workshop or mod activity is present in the recent output. Watch for missing item, checksum, or map loading issues.";
            }

            return lineCount == 0
                ? "The server reports as live, but the buffer is still empty. Keep this page open while startup finishes."
                : "Keep this console open during joins, config reloads, and mod validation to watch the live feed.";
        }

        return "The server is not currently running. Start it from Overview or Install & Update, then return here to inspect startup output.";
    }

    private static string BuildRuntimeWindowSummary(ServerRuntimeStatus? status)
    {
        if (status is null)
        {
            return "Runtime window: no status is available yet.";
        }

        return status.State switch
        {
            ServerRuntimeState.Running when status.StartedAtUtc is not null
                => $"Runtime window: running since {status.StartedAtUtc.Value:yyyy-MM-dd HH:mm 'UTC'}.",
            ServerRuntimeState.Stopped when status.StoppedAtUtc is not null && !string.IsNullOrWhiteSpace(status.LastExitReason)
                => $"Runtime window: stopped at {status.StoppedAtUtc.Value:yyyy-MM-dd HH:mm 'UTC'} | last exit: {status.LastExitReason}.",
            ServerRuntimeState.Stopped when status.StoppedAtUtc is not null
                => $"Runtime window: stopped at {status.StoppedAtUtc.Value:yyyy-MM-dd HH:mm 'UTC'}.",
            _ => $"Runtime window: {status.State}.",
        };
    }

    private static bool IsErrorSignal(string line) =>
        line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarningSignal(string line) =>
        line.Contains("warn", StringComparison.OrdinalIgnoreCase);

    private static bool IsModSignal(string line) =>
        line.Contains("mod", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("workshop", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("steam", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("map", StringComparison.OrdinalIgnoreCase);
}
