using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Act;
using FliegenPilz.Net;
using FliegenPilz.Util;

namespace FliegenPilz.World.Sessions;

/// <summary>Runs scheduled room actions using the shared tick notifier.</summary>
public sealed class RoomTimer<TSession> : IDisposable where TSession : IGameSession
{
    private readonly RoomExecutor<TSession> _executor;
    private readonly TickNotifier _notifier;
    private readonly DelayQueue<Func<Ticks, CancellationToken, ValueTask>> _queue = new();
    private readonly List<Func<Ticks, CancellationToken, ValueTask>> _dispatchBuffer = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _runner;

    public RoomTimer(RoomExecutor<TSession> executor, TickNotifier notifier)
    {
        _executor = executor;
        _notifier = notifier;
        _runner = Task.Run(RunAsync);
    }

    public void ScheduleAt(Ticks dueTick, Func<Ticks, CancellationToken, ValueTask> action)
    {
        _queue.Enqueue(dueTick, action);
    }

    public void ScheduleAfterMilliseconds(ulong delayMs, Func<Ticks, CancellationToken, ValueTask> action)
    {
        var due = _notifier.LastTick + delayMs;
        ScheduleAt(due, action);
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var tick = await _notifier.WaitNextAsync(_cts.Token).ConfigureAwait(false);
                var due = _queue.DrainDue(tick, _dispatchBuffer);
                if (due.Count == 0)
                    continue;

                foreach (var action in due)
                {
                    var command = new RoomActionCommand<TSession>(action);
                    if (!_executor.TryPost(command))
                    {
                        await _executor.PostAsync(command, _cts.Token).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _runner.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts.Dispose();
        }
    }
}

public sealed class RoomRuntime<TSession> where TSession : IGameSession
{
    internal RoomRuntime(RoomExecutor<TSession> executor, RoomTimer<TSession> timer)
    {
        Executor = executor;
        Timer = timer;
    }

    public RoomExecutor<TSession> Executor { get; }
    public RoomTimer<TSession> Timer { get; }

    public void ScheduleAt(Ticks dueTick, Func<Ticks, CancellationToken, ValueTask> action) => Timer.ScheduleAt(dueTick, action);

    public void ScheduleAfterMilliseconds(ulong delayMs, Func<Ticks, CancellationToken, ValueTask> action) => Timer.ScheduleAfterMilliseconds(delayMs, action);
}
