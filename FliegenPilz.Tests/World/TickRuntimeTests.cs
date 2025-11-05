using System;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Act;
using FliegenPilz.Net;
using FliegenPilz.Util;
using FliegenPilz.World;
using FliegenPilz.World.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FliegenPilz.Tests.World;

public sealed class TickRuntimeTests : IAsyncLifetime
{
    private readonly TickNotifier _notifier = new();
    private readonly GlobalClock _clock = new();
    private readonly TickScheduler _scheduler;
    private readonly RoomServer _roomServer;

    public TickRuntimeTests()
    {
        var options = Options.Create(new TickSchedulerOptions
        {
            TickInterval = TimeSpan.FromMilliseconds(5)
        });

        _scheduler = new TickScheduler(_clock, options, NullLogger<TickScheduler>.Instance, _notifier);
        _roomServer = new RoomServer(_scheduler, _notifier);
    }

    public async Task InitializeAsync()
    {
        await _scheduler.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _scheduler.StopAsync(CancellationToken.None);
        _roomServer.Dispose();
        _notifier.Dispose();
    }

    [Fact]
    public async Task RoomTimerExecutesScheduledAction()
    {
        var world = _roomServer.CreateWorld(new WorldId(1), "TestWorld");
        var channel = _roomServer.CreateChannel(world, new ChannelId(1), "TestChannel");
        var room = _roomServer.CreateRoom<TestSession>(channel, new RoomId(1, new MapId(1)), "TestRoom");

        var completion = new TaskCompletionSource<Ticks>(TaskCreationOptions.RunContinuationsAsynchronously);

        room.ScheduleAfterMilliseconds(20, (tick, ct) =>
        {
            completion.TrySetResult(tick);
            return ValueTask.CompletedTask;
        });

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(result.Milliseconds >= 20UL);
    }

    private sealed class TestSession : GameSessionBase
    {
    }
}
