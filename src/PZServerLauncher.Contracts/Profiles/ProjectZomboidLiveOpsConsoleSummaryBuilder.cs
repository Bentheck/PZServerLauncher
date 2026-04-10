using PZServerLauncher.Core.Planning;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProjectZomboidLiveOpsConsoleSummary(
    string FeedHeadline,
    string IncidentHeadline,
    string RosterHeadline,
    string CommandHeadline,
    string OperatorSummary,
    string TriageSummary,
    IReadOnlyList<string> Checklist);

public static class ProjectZomboidLiveOpsConsoleSummaryBuilder
{
    public static ProjectZomboidLiveOpsConsoleSummary Build(
        ProjectZomboidLogPostureSummary posture,
        string runtimeState,
        bool canSendCommands,
        int inferredRosterCount,
        int recentOperatorActionCount)
    {
        var running = string.Equals(runtimeState, "Running", StringComparison.OrdinalIgnoreCase);

        var feedHeadline = posture.BufferedLineCount == 0
            ? "No live feed buffered yet."
            : posture.HasErrorSignals
                ? "Errors are present in the live buffer."
                : posture.HasWarningSignals
                    ? "Warnings are present in the live buffer."
                    : "Live feed is active and looks stable.";

        var incidentHeadline = posture.HasErrorSignals
            ? $"{posture.ErrorSignalCount} error signal(s) need operator attention."
            : posture.HasWarningSignals
                ? $"{posture.WarningSignalCount} warning signal(s) should be reviewed."
                : posture.HasModSignals
                    ? "Recent output includes workshop/mod chatter."
                    : "No obvious incident signals are buffered right now.";

        var rosterHeadline = inferredRosterCount > 0
            ? $"{inferredRosterCount} player(s) are currently inferred online."
            : posture.ConnectedPlayerCount > 0
                ? $"{posture.ConnectedPlayerCount} player(s) have been inferred from the buffer."
                : "No active roster is inferred yet.";

        var commandHeadline = canSendCommands
            ? recentOperatorActionCount > 0
                ? $"{recentOperatorActionCount} recent operator action(s) logged while live control is enabled."
                : "Live control is unlocked. Broadcast and raw console actions are available."
            : "Monitor-only mode until the runtime is live.";

        var operatorSummary = BuildOperatorSummary(
            posture,
            running,
            canSendCommands,
            inferredRosterCount,
            recentOperatorActionCount);

        var triageSummary = posture.HasErrorSignals
            ? "Start with the latest errors, then review the live roster and operator actions before issuing more commands."
            : posture.HasWarningSignals
                ? "Clear the warning posture before the next risky admin or reload action."
                : posture.HasModSignals
                    ? "Keep an eye on workshop and mod chatter so you catch map, checksum, or missing-content problems early."
                    : "Use this page to watch joins, moderation actions, and save/reload commands in real time.";

        return new ProjectZomboidLiveOpsConsoleSummary(
            feedHeadline,
            incidentHeadline,
            rosterHeadline,
            commandHeadline,
            operatorSummary,
            triageSummary,
            BuildChecklist(posture, running, canSendCommands, inferredRosterCount));
    }

    public static ProjectZomboidLiveOpsConsoleSummary Empty() =>
        Build(
            new ProjectZomboidLogPostureSummary(
                "No buffer.",
                "No signal.",
                "No posture.",
                "No operator focus.",
                "No runtime window.",
                "No player activity.",
                "No operator commands.",
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false,
                [],
                []),
            runtimeState: "Stopped",
            canSendCommands: false,
            inferredRosterCount: 0,
            recentOperatorActionCount: 0);

    private static string BuildOperatorSummary(
        ProjectZomboidLogPostureSummary posture,
        bool running,
        bool canSendCommands,
        int inferredRosterCount,
        int recentOperatorActionCount)
    {
        if (!running)
        {
            return "The server is not live yet. Start it from Overview or Install & Update, then return here to validate startup and player joins.";
        }

        if (posture.HasErrorSignals)
        {
            return "The server is live, but the buffer already contains failure signals. Triage those first before you issue more commands or moderation actions.";
        }

        if (posture.HasModSignals)
        {
            return "Workshop or mod chatter is present in the live feed. Watch the next few lines carefully if you are validating a new content stack.";
        }

        if (inferredRosterCount > 0 && canSendCommands)
        {
            return "The live roster is populated and control actions are available. This is the right time to moderate, broadcast, or request a save.";
        }

        if (canSendCommands)
        {
            return "The server is live and ready for console actions, but the roster is still sparse. Pull the player list if you need a fresher view.";
        }

        return recentOperatorActionCount > 0
            ? "Recent operator actions are visible, but the page is back in monitor-only mode until the runtime is live again."
            : "Use this page as a monitor until the runtime becomes live enough for direct console actions.";
    }

    private static IReadOnlyList<string> BuildChecklist(
        ProjectZomboidLogPostureSummary posture,
        bool running,
        bool canSendCommands,
        int inferredRosterCount)
    {
        var checklist = new List<string>();

        if (!running)
        {
            checklist.Add("Start or reload the server before you rely on this page for live operational decisions.");
        }
        else if (posture.BufferedLineCount == 0)
        {
            checklist.Add("Keep the page open while startup completes so the host can buffer fresh runtime output.");
        }

        if (posture.HasErrorSignals)
        {
            checklist.Add("Read the latest error lines before issuing more save, broadcast, or moderation commands.");
        }

        if (posture.HasWarningSignals && !posture.HasErrorSignals)
        {
            checklist.Add("Clear the warning posture before the next risky configuration or content change.");
        }

        if (inferredRosterCount == 0)
        {
            checklist.Add("Request the player list during live runtime if you need a fresher roster sample.");
        }

        if (!canSendCommands)
        {
            checklist.Add("Broadcast, save, and raw console actions unlock only when the runtime is live.");
        }

        if (posture.HasModSignals)
        {
            checklist.Add("Watch for workshop, map, or checksum fallout in the next few log lines.");
        }

        if (checklist.Count == 0)
        {
            checklist.Add("Keep Overview and Logs open together during busy sessions so runtime state and live feed stay in sync.");
        }

        return checklist;
    }
}
