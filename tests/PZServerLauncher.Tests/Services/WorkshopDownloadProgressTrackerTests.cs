using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class WorkshopDownloadProgressTrackerTests
{
    [Fact]
    public void Observe_UsesConfiguredWorkshopOrderToBuildProgress()
    {
        var tracker = new WorkshopDownloadProgressTracker(["111111111", "222222222", "333333333"]);

        var progress = tracker.Observe(
            "Downloading workshop content for 222222222 at 1048576 bytes.",
            DateTimeOffset.UtcNow);

        Assert.NotNull(progress);
        Assert.Equal(2, progress.CurrentItemIndex);
        Assert.Equal(3, progress.TotalItemCount);
        Assert.Equal("222222222", progress.CurrentWorkshopId);
        Assert.False(progress.IsComplete);
    }

    [Fact]
    public void Observe_IgnoresNonConfiguredByteCounters()
    {
        var tracker = new WorkshopDownloadProgressTracker(["111111111", "222222222"]);

        var progress = tracker.Observe(
            "Downloading 999999999 bytes from the workshop cache.",
            DateTimeOffset.UtcNow);

        Assert.Null(progress);
    }

    [Fact]
    public void Observe_DoesNotResetToEarlierWorkshopItems()
    {
        var tracker = new WorkshopDownloadProgressTracker(["111111111", "222222222", "333333333"]);

        tracker.Observe("Downloading workshop content for 333333333.", DateTimeOffset.UtcNow);
        var progress = tracker.Observe("Downloading workshop content for 111111111.", DateTimeOffset.UtcNow);

        Assert.NotNull(progress);
        Assert.Equal(3, progress.CurrentItemIndex);
        Assert.Equal("333333333", progress.CurrentWorkshopId);
    }

    [Fact]
    public void Observe_MarksFinalConfiguredWorkshopAsCompleteWhenCompletionSignalAppears()
    {
        var tracker = new WorkshopDownloadProgressTracker(["111111111", "222222222", "333333333"]);

        var progress = tracker.Observe(
            "Workshop item 333333333 download completed successfully.",
            DateTimeOffset.UtcNow);

        Assert.NotNull(progress);
        Assert.Equal(3, progress.CurrentItemIndex);
        Assert.True(progress.IsComplete);
        Assert.Equal("Workshop download complete (3/3) | Workshop ID 333333333", progress.DetailLabel);
    }
}
