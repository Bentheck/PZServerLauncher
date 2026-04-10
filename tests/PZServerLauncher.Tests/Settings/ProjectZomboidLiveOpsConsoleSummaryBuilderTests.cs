using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Planning;

namespace PZServerLauncher.Tests.Settings;

public sealed class ProjectZomboidLiveOpsConsoleSummaryBuilderTests
{
    [Fact]
    public void Build_SummarizesActiveRuntimeWithErrors()
    {
        var posture = new ProjectZomboidLogPostureSummary(
            "Rolling runtime buffer currently holds 25 line(s).",
            "Latest signal: ERROR connection reset.",
            "Recent buffer contains 2 error or failure signal(s).",
            "The server is live, but recent output contains failure signals.",
            "Runtime window: running.",
            "Inferred live roster: Alice, Bob.",
            "No launcher-issued console commands are visible in the current buffer.",
            25,
            2,
            1,
            0,
            2,
            0,
            true,
            true,
            false,
            ["Alice", "Bob"],
            ["Alice connected"]);

        var summary = ProjectZomboidLiveOpsConsoleSummaryBuilder.Build(
            posture,
            runtimeState: "Running",
            canSendCommands: true,
            inferredRosterCount: 2,
            recentOperatorActionCount: 1);

        Assert.Equal("Errors are present in the live buffer.", summary.FeedHeadline);
        Assert.Contains("error signal", summary.IncidentHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 player(s)", summary.RosterHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("live control", summary.CommandHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("latest errors", summary.TriageSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_SummarizesMonitorOnlyStoppedRuntime()
    {
        var posture = new ProjectZomboidLogPostureSummary(
            "No buffer.",
            "No signal.",
            "No posture.",
            "Operator focus unavailable.",
            "Runtime window unavailable.",
            "No players.",
            "No commands.",
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
            []);

        var summary = ProjectZomboidLiveOpsConsoleSummaryBuilder.Build(
            posture,
            runtimeState: "Stopped",
            canSendCommands: false,
            inferredRosterCount: 0,
            recentOperatorActionCount: 0);

        Assert.Contains("No live feed", summary.FeedHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("monitor-only", summary.CommandHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not live yet", summary.OperatorSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(summary.Checklist, item => item.Contains("Start or reload the server", StringComparison.OrdinalIgnoreCase));
    }
}
