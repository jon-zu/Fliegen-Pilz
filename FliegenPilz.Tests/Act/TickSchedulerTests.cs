using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FliegenPilz.Act;
using FliegenPilz.Util;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FliegenPilz.Tests.Act;

public class TickSchedulerTests : IAsyncLifetime
{
    private readonly TickNotifier _notifier = new();
    private readonly GlobalClock _clock = new();
    private readonly TickScheduler _scheduler;

    public TickSchedulerTests()
    {
        var options = Options.Create(new TickSchedulerOptions
        {
            TickInterval = TimeSpan.FromMilliseconds(5)
        });

        _scheduler = new TickScheduler(_clock, options, NullLogger<TickScheduler>.Instance, _notifier);
    }

    public async Task InitializeAsync()
    {
        await _scheduler.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _scheduler.StopAsync(CancellationToken.None);
        _notifier.Dispose();
    }

    [Fact]
    public async Task ProcessesMessagesBeforeTickAndNotifiesTickEnd()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var actor = new TestActor();
        using var registration = _scheduler.Register(actor);

        await actor.PostAsync("hello", cts.Token);

        var events = await actor.WaitForEventsAsync(3, cts.Token);

        Assert.Equal(new[] { "message:hello", "tick", "end" }, events);
    }

    private sealed class TestActor : TickActor<string>
    {
        private readonly Channel<string> _events = Channel.CreateUnbounded<string>();
        private volatile bool _armed;

        public TestActor() : base("TestActor")
        {
        }

        protected override ValueTask OnMessageAsync(string message, Ticks now, CancellationToken ct)
        {
            _armed = true;
            _events.Writer.TryWrite($"message:{message}");
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnTickCoreAsync(Ticks now, CancellationToken ct)
        {
            if (_armed)
            {
                _events.Writer.TryWrite("tick");
            }
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnTickEndAsync(Ticks now, CancellationToken ct)
        {
            if (_armed)
            {
                _events.Writer.TryWrite("end");
                _armed = false;
            }
            return ValueTask.CompletedTask;
        }

        public async Task<string[]> WaitForEventsAsync(int count, CancellationToken ct)
        {
            var list = new List<string>(count);
            while (list.Count < count)
            {
                list.Add(await _events.Reader.ReadAsync(ct));
            }
            return list.ToArray();
        }
    }
}
