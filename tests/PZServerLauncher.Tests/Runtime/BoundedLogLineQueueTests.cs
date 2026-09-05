using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Runtime;

public sealed class BoundedLogLineQueueTests
{
    [Fact]
    public void Enqueue_DropsOldestLinesAtCapacity()
    {
        var queue = new BoundedLogLineQueue(3);

        queue.Enqueue("two lines old");
        queue.Enqueue("one line old");
        queue.Enqueue("current");
        queue.Enqueue("newest");

        Assert.Equal(["one line old", "current", "newest"], queue.Drain(10));
        Assert.Equal(1, queue.DroppedLineCount);
    }

    [Fact]
    public void Drain_ReturnsBoundedBatchesInOrder()
    {
        var queue = new BoundedLogLineQueue(10);
        for (var index = 0; index < 7; index++)
        {
            queue.Enqueue($"line-{index}");
        }

        Assert.Equal(["line-0", "line-1", "line-2"], queue.Drain(3));
        Assert.Equal(["line-3", "line-4", "line-5", "line-6"], queue.Drain(10));
        Assert.Empty(queue.Drain(10));
    }

    [Fact]
    public void Clear_RemovesPendingLinesAndResetsDropCount()
    {
        var queue = new BoundedLogLineQueue(1);
        queue.Enqueue("old");
        queue.Enqueue("new");

        queue.Clear();

        Assert.Equal(0, queue.Count);
        Assert.Equal(0, queue.DroppedLineCount);
    }

    [Fact]
    public void Enqueue_RemainsBoundedDuringConcurrentLogBurst()
    {
        const int capacity = 500;
        const int lineCount = 20_000;
        var queue = new BoundedLogLineQueue(capacity);

        Parallel.For(0, lineCount, index => queue.Enqueue($"line-{index}"));

        Assert.Equal(capacity, queue.Count);
        Assert.Equal(lineCount - capacity, queue.DroppedLineCount);
        Assert.Equal(capacity, queue.Drain(capacity).Count);
    }
}
