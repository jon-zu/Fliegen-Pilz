using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Util;

namespace FliegenPilz.Act;

/// <summary>
/// Provides async notifications whenever the simulation advances by one tick.
/// </summary>
public sealed class TickNotifier : IDisposable
{
    private readonly object _gate = new();
    private readonly List<Waiter> _waiters = new();
    private bool _disposed;
    private Ticks _lastTick = Ticks.Zero;

    /// <summary>Returns the most recent tick that was published.</summary>
    public Ticks LastTick
    {
        get
        {
            lock (_gate)
            {
                return _lastTick;
            }
        }
    }

    /// <summary>
    /// Await the next published tick. If multiple callers await concurrently, all receive the same next tick.
    /// </summary>
    public ValueTask<Ticks> WaitNextAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            var tcs = new TaskCompletionSource<Ticks>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiter = new Waiter(tcs);
            if (ct.CanBeCanceled)
            {
                waiter.Cancellation = ct.Register(state =>
                {
                    var (owner, source) = ((TickNotifier, TaskCompletionSource<Ticks>))state!;
                    owner.CancelWaiter(source);
                }, (this, tcs));
            }

            _waiters.Add(waiter);
            return new ValueTask<Ticks>(tcs.Task);
        }
    }

    internal void Publish(Ticks tick)
    {
        Waiter[] pending;
        lock (_gate)
        {
            if (_disposed) return;

            _lastTick = tick;
            if (_waiters.Count == 0) return;
            pending = _waiters.ToArray();
            _waiters.Clear();
        }

        foreach (ref readonly var waiter in pending.AsSpan())
        {
            waiter.Cancellation.Dispose();
            waiter.Source.TrySetResult(tick);
        }
    }

    private void CancelWaiter(TaskCompletionSource<Ticks> tcs)
    {
        lock (_gate)
        {
            if (_disposed) return;

            var index = _waiters.FindIndex(w => ReferenceEquals(w.Source, tcs));
            if (index >= 0)
            {
                var waiter = _waiters[index];
                _waiters.RemoveAt(index);
                waiter.Cancellation.Dispose();
                tcs.TrySetCanceled();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TickNotifier));
    }

    public void Dispose()
    {
        Waiter[] pending;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            pending = _waiters.ToArray();
            _waiters.Clear();
        }

        foreach (ref readonly var waiter in pending.AsSpan())
        {
            waiter.Cancellation.Dispose();
            waiter.Source.TrySetCanceled();
        }
    }

    private sealed class Waiter
    {
        public Waiter(TaskCompletionSource<Ticks> source)
        {
            Source = source;
        }

        public TaskCompletionSource<Ticks> Source { get; }
        public CancellationTokenRegistration Cancellation;
    }
}
