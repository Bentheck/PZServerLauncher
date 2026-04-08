using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Runtime;
using System.Text.RegularExpressions;

namespace PZServerLauncher.Infrastructure.Planning;

public static class ProjectZomboidLogPostureSummaryBuilder
{
    private static readonly Regex QuotedNameRegex = new("""(?<name>[^"]+)""", RegexOptions.Compiled);
    private static readonly Regex UsernameRegex = new("""\b(?:username|user|player)[=: ]+["']?(?<name>[A-Za-z0-9 _.\-]+)["']?""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] PlayerJoinKeywords = ["connected", "joined", "logged in"];
    private static readonly string[] PlayerLeaveKeywords = ["disconnected", "disconnect", "left", "kicked", "banned"];

    public static ProjectZomboidLogPostureSummary Build(ServerRuntimeStatus? status, IReadOnlyList<string>? logLines)
    {
        var lines = (logLines ?? []).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var latestLine = status?.LatestLogLine ?? lines.LastOrDefault();
        var errorCount = lines.Count(IsErrorSignal);
        var warningCount = lines.Count(IsWarningSignal);
        var modCount = lines.Count(IsModSignal);
        var operatorCommands = lines.Where(IsLauncherCommand).ToList();
        var recentPlayerSignals = lines.Where(IsPlayerSignal).TakeLast(8).ToList();
        var connectedPlayers = BuildConnectedPlayers(lines);

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
        var playerActivitySummary = BuildPlayerActivitySummary(status, connectedPlayers, recentPlayerSignals);
        var operatorCommandSummary = !string.IsNullOrWhiteSpace(status?.LastOperatorCommandSummary)
            ? status.LastOperatorCommandSummary!
            : operatorCommands.Count == 0
                ? "No launcher-issued console commands are visible in the current buffer."
                : $"{operatorCommands.Count} launcher command(s) echoed in the current buffer. Latest: {operatorCommands[^1]}";

        return new ProjectZomboidLogPostureSummary(
            bufferSummary,
            latestSignalSummary,
            signalPostureSummary,
            operatorFocusSummary,
            runtimeWindowSummary,
            playerActivitySummary,
            operatorCommandSummary,
            lines.Count,
            errorCount,
            warningCount,
            modCount,
            connectedPlayers.Count,
            operatorCommands.Count,
            errorCount > 0,
            warningCount > 0,
            modCount > 0,
            connectedPlayers,
            recentPlayerSignals);
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

    private static bool IsLauncherCommand(string line) =>
        line.StartsWith("[launcher] >", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlayerSignal(string line) =>
        line.Contains("player", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("user", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("connect", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("disconnect", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("joined", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("left", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("kicked", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("whitelist", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildConnectedPlayers(IReadOnlyList<string> lines)
    {
        var players = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (!TryParsePlayerTransition(line, out var playerName, out var isConnected))
            {
                continue;
            }

            players[playerName] = isConnected;
        }

        return players
            .Where(entry => entry.Value)
            .Select(entry => entry.Key)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildPlayerActivitySummary(ServerRuntimeStatus? status, IReadOnlyList<string> connectedPlayers, IReadOnlyList<string> recentPlayerSignals)
    {
        if (connectedPlayers.Count > 0)
        {
            return $"Inferred live roster: {string.Join(", ", connectedPlayers)}.";
        }

        if (status?.ConnectedPlayerCount > 0)
        {
            return $"{status.ConnectedPlayerCount} player(s) are currently inferred online from the live runtime session.";
        }

        if (recentPlayerSignals.Count > 0)
        {
            return "Recent player-related signals are present, but no active roster could be inferred from the current buffer.";
        }

        return "No player activity signals are buffered yet. Keep the live console open during joins, disconnects, or after sending the players command.";
    }

    private static bool TryParsePlayerTransition(string line, out string playerName, out bool isConnected)
    {
        playerName = string.Empty;
        isConnected = false;

        if (IsLauncherCommand(line))
        {
            return false;
        }

        if (!TryClassifyPlayerTransition(line, out isConnected))
        {
            return false;
        }

        playerName = ExtractPlayerName(line);
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return false;
        }

        return true;
    }

    private static bool TryClassifyPlayerTransition(string line, out bool isConnected)
    {
        foreach (var keyword in PlayerLeaveKeywords)
        {
            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                isConnected = false;
                return true;
            }
        }

        foreach (var keyword in PlayerJoinKeywords)
        {
            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                isConnected = true;
                return true;
            }
        }

        isConnected = false;
        return false;
    }

    private static string ExtractPlayerName(string line)
    {
        var quotedMatch = QuotedNameRegex.Match(line);
        if (quotedMatch.Success)
        {
            return NormalizePlayerName(quotedMatch.Groups["name"].Value);
        }

        var usernameMatch = UsernameRegex.Match(line);
        if (usernameMatch.Success)
        {
            return NormalizePlayerName(usernameMatch.Groups["name"].Value);
        }

        var transitionMarkerIndex = line.IndexOf(" connected", StringComparison.OrdinalIgnoreCase);
        transitionMarkerIndex = transitionMarkerIndex >= 0
            ? transitionMarkerIndex
            : line.IndexOf(" disconnected", StringComparison.OrdinalIgnoreCase);
        transitionMarkerIndex = transitionMarkerIndex >= 0
            ? transitionMarkerIndex
            : line.IndexOf(" joined", StringComparison.OrdinalIgnoreCase);
        transitionMarkerIndex = transitionMarkerIndex >= 0
            ? transitionMarkerIndex
            : line.IndexOf(" left", StringComparison.OrdinalIgnoreCase);
        transitionMarkerIndex = transitionMarkerIndex >= 0
            ? transitionMarkerIndex
            : line.IndexOf(" kicked", StringComparison.OrdinalIgnoreCase);

        if (transitionMarkerIndex <= 0)
        {
            return string.Empty;
        }

        var prefix = line[..transitionMarkerIndex].Trim();
        if (prefix.Contains(':'))
        {
            prefix = prefix[(prefix.LastIndexOf(':') + 1)..].Trim();
        }

        var tokens = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        return NormalizePlayerName(tokens[^1]);
    }

    private static string NormalizePlayerName(string value) =>
        value.Trim().Trim('"', '\'', '[', ']', '(', ')', ',', '.', ';', ':');
}
