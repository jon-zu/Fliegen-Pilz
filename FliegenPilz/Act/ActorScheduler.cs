using System.Collections.Generic;
using System.Threading.Channels;
using FliegenPilz.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FliegenPilz.Act;

public sealed class TickSchedulerOptions
{
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMilliseconds(50);
}

public interface ITickActor
{
    string Name { get; }
    ValueTask OnTickAsync(Ticks now, CancellationToken ct);
    ValueTask OnTickEndAsync(Ticks now, CancellationToken ct);
}

public abstract class TickActor<TMessage> : ITickActor
{
    private readonly Channel<TMessage> _mailbox;

    protected TickActor(string name, int mailboxCapacity = 1024)
    {
        Name = name;
        _mailbox = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(mailboxCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public string Name { get; }

    public bool TryPost(TMessage message) => _mailbox.Writer.TryWrite(message);

    public ValueTask PostAsync(TMessage message, CancellationToken ct = default) => _mailbox.Writer.WriteAsync(message, ct);

    async ValueTask ITickActor.OnTickAsync(Ticks now, CancellationToken ct)
    {
        while (_mailbox.Reader.TryRead(out var msg))
        {
            await OnMessageAsync(msg, now, ct).ConfigureAwait(false);
        }
        await OnTickCoreAsync(now, ct).ConfigureAwait(false);
    }

    ValueTask ITickActor.OnTickEndAsync(Ticks now, CancellationToken ct) => OnTickEndAsync(now, ct);

    protected virtual ValueTask OnMessageAsync(TMessage message, Ticks now, CancellationToken ct) => ValueTask.CompletedTask;

    protected virtual ValueTask OnTickCoreAsync(Ticks now, CancellationToken ct) => ValueTask.CompletedTask;

    protected virtual ValueTask OnTickEndAsync(Ticks now, CancellationToken ct) => ValueTask.CompletedTask;
}

public sealed class TickScheduler : IHostedService, IDisposable
{
    private readonly GlobalClock _clock;
    private readonly TickSchedulerOptions _options;
    private readonly ILogger<TickScheduler> _logger;
    private readonly TickNotifier? _notifier;
    private readonly object _gate = new();
    private readonly List<ITickActor> _actors = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public TickScheduler(GlobalClock clock, IOptions<TickSchedulerOptions> options, ILogger<TickScheduler> logger, TickNotifier? notifier = null)
    {
        _clock = clock;
        _options = options.Value;
        _logger = logger;
        _notifier = notifier;
        if (_options.TickInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Tick interval must be positive.");
    }

    public IDisposable Register(ITickActor actor)
    {
        lock (_gate)
        {
            _actors.Add(actor);
        }
        _logger.LogInformation("Registered tick actor {Actor}", actor.Name);
        return new Subscription(this, actor);
    }

    private void Unregister(ITickActor actor)
    {
        lock (_gate)
        {
            _actors.Remove(actor);
        }
        _logger.LogInformation("Unregistered tick actor {Actor}", actor.Name);
    }

    private ITickActor[] SnapshotActors()
    {
        lock (_gate)
        {
            return _actors.ToArray();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop != null)
            throw new InvalidOperationException("TickScheduler already started.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        _logger.LogInformation("TickScheduler loop started with interval {Interval}ms", _options.TickInterval.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loop == null || _cts == null)
            return;

        _cts.Cancel();
        try
        {
            await Task.WhenAny(_loop, Task.Delay(Timeout.Infinite, cancellationToken));
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _cts.Dispose();
            _loop.Dispose();
            _cts = null;
            _loop = null;
        }
        _logger.LogInformation("TickScheduler loop stopped");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var intervalTicks = Ticks.FromTimeSpan(_options.TickInterval);
        var tickTime = _clock.Now;

        while (!ct.IsCancellationRequested)
        {
            var actors = SnapshotActors();
            foreach (var actor in actors)
            {
                try
                {
                    await actor.OnTickAsync(tickTime, ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error during OnTick for actor {Actor}", actor.Name);
                }
            }

            foreach (var actor in actors)
            {
                try
                {
                    await actor.OnTickEndAsync(tickTime, ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error during OnTickEnd for actor {Actor}", actor.Name);
                }
            }

            _notifier?.Publish(tickTime);

            tickTime += intervalTicks;
            var now = _clock.Now;
            var wait = tickTime - now;
            if (wait.Milliseconds > 0)
            {
                try
                {
                    var delayMs = wait.Milliseconds > int.MaxValue ? int.MaxValue : (int)wait.Milliseconds;
                    await Task.Delay(delayMs, ct);
                }
                catch (TaskCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _loop?.Dispose();
        _cts?.Dispose();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly TickScheduler _owner;
        private ITickActor? _actor;

        public Subscription(TickScheduler owner, ITickActor actor)
        {
            _owner = owner;
            _actor = actor;
        }

        public void Dispose()
        {
            var actor = Interlocked.Exchange(ref _actor, null);
            if (actor != null)
            {
                _owner.Unregister(actor);
            }
        }
    }
}
