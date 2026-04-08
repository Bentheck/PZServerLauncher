using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Tests.Planning;

public sealed class ProjectZomboidLogPostureSummaryBuilderTests
{
    [Fact]
    public void Build_ReportsErrorHeavyRunningBuffer()
    {
        var status = new ServerRuntimeStatus(
            "servertest",
            ServerRuntimeState.Running,
            1234,
            DateTimeOffset.Parse("2026-04-08T14:00:00Z"),
            null,
            null,
            "ERROR Workshop item failed to load");

        var lines = new[]
        {
            "INFO Server started",
            "WARN Mod checksum mismatch",
            "ERROR Workshop item failed to load",
        };

        var summary = ProjectZomboidLogPostureSummaryBuilder.Build(status, lines);

        Assert.Equal(3, summary.BufferedLineCount);
        Assert.Equal(1, summary.ErrorSignalCount);
        Assert.Equal(1, summary.WarningSignalCount);
        Assert.Equal(2, summary.ModSignalCount);
        Assert.Contains("3 line(s)", summary.BufferSummary);
        Assert.Contains("Latest signal: ERROR Workshop item failed to load", summary.LatestSignalSummary);
        Assert.Contains("1 error or failure signal", summary.SignalPostureSummary);
        Assert.Contains("Investigate", summary.OperatorFocusSummary);
        Assert.Contains("running since", summary.RuntimeWindowSummary);
    }

    [Fact]
    public void Build_ReportsIdleBufferWhenServerIsStopped()
    {
        var status = new ServerRuntimeStatus(
            "servertest",
            ServerRuntimeState.Stopped,
            null,
            null,
            DateTimeOffset.Parse("2026-04-08T15:00:00Z"),
            "clean shutdown",
            null);

        var summary = ProjectZomboidLogPostureSummaryBuilder.Build(status, []);

        Assert.Equal(0, summary.BufferedLineCount);
        Assert.Contains("No buffered lines are available yet", summary.BufferSummary);
        Assert.Contains("no runtime output captured yet", summary.LatestSignalSummary);
        Assert.Contains("No runtime signals are buffered yet", summary.SignalPostureSummary);
        Assert.Contains("not currently running", summary.OperatorFocusSummary);
        Assert.Contains("clean shutdown", summary.RuntimeWindowSummary);
    }
}
