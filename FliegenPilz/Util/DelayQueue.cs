using System.Collections.Generic;

namespace FliegenPilz.Util;

/// <summary>
/// Thread-safe priority queue keyed by <see cref="Ticks"/> that returns all entries due on or before a target time.
/// </summary>
/// <typeparam name="T">Payload type stored in the queue.</typeparam>
public sealed class DelayQueue<T>
{
    private readonly PriorityQueue<T, ulong> _queue = new();
    private readonly object _gate = new();

    /// <summary>Number of scheduled entries (approximate; may be stale if examined without synchronization).</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>Schedule <paramref name="value"/> to execute at <paramref name="dueTick"/>.</summary>
    public void Enqueue(Ticks dueTick, T value)
    {
        lock (_gate)
        {
            _queue.Enqueue(value, dueTick.Milliseconds);
        }
    }

    /// <summary>
    /// Move all entries whose due time is &lt;= <paramref name="now"/> into <paramref name="buffer"/>.
    /// </summary>
    public List<T> DrainDue(Ticks now, List<T>? buffer = null)
    {
        buffer ??= new List<T>();
        buffer.Clear();

        lock (_gate)
        {
            while (_queue.TryPeek(out _, out var priority) && priority <= now.Milliseconds)
            {
                buffer.Add(_queue.Dequeue());
            }
        }

        return buffer;
    }

    /// <summary>Remove all scheduled entries.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _queue.Clear();
        }
    }
}
