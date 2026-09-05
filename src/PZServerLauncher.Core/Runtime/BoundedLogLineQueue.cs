namespace PZServerLauncher.Core.Runtime;

public sealed class BoundedLogLineQueue
{
    private readonly object _gate = new();
    private readonly Queue<string> _lines;
    private readonly int _capacity;
    private long _droppedLineCount;

    public BoundedLogLineQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _lines = new Queue<string>(capacity);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _lines.Count;
            }
        }
    }

    public long DroppedLineCount
    {
        get
        {
            lock (_gate)
            {
                return _droppedLineCount;
            }
        }
    }

    public void Enqueue(string line)
    {
        lock (_gate)
        {
            if (_lines.Count == _capacity)
            {
                _lines.Dequeue();
                _droppedLineCount++;
            }

            _lines.Enqueue(line);
        }
    }

    public IReadOnlyList<string> Drain(int maximumLineCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLineCount, 1);

        lock (_gate)
        {
            var count = Math.Min(maximumLineCount, _lines.Count);
            if (count == 0)
            {
                return [];
            }

            var result = new string[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = _lines.Dequeue();
            }

            return result;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _lines.Clear();
            _droppedLineCount = 0;
        }
    }
}
