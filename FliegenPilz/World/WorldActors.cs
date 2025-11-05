using System.Collections.Generic;
using System.Linq;
using FliegenPilz.Act;
using FliegenPilz.Util;

namespace FliegenPilz.World;

public interface IWorldCommand;

public sealed record WorldRegisterChannel(ChannelActor Channel) : IWorldCommand;
public sealed record WorldRemoveChannel(ChannelId ChannelId) : IWorldCommand;
public sealed record WorldActionCommand(Func<Ticks, CancellationToken, ValueTask> Action) : IWorldCommand;

public sealed class WorldActor : TickActor<IWorldCommand>
{
    private readonly Dictionary<ChannelId, ChannelActor> _channels = new();
    private readonly List<Func<Ticks, CancellationToken, ValueTask>> _deferred = new();

    public WorldActor(WorldId id, string name, int mailboxCapacity = 1024)
        : base($"World[{id}:{name}]", mailboxCapacity)
    {
        Id = id;
        DisplayName = name;
    }

    public WorldId Id { get; }
    public string DisplayName { get; }

    public ChannelActor[] Channels
    {
        get
        {
            lock (_channels)
            {
                return _channels.Values.ToArray();
            }
        }
    }

    public ValueTask RegisterChannelAsync(ChannelActor channel, CancellationToken ct = default) =>
        PostAsync(new WorldRegisterChannel(channel), ct);

    public ValueTask RemoveChannelAsync(ChannelId channelId, CancellationToken ct = default) =>
        PostAsync(new WorldRemoveChannel(channelId), ct);

    public ValueTask ScheduleAsync(Func<Ticks, CancellationToken, ValueTask> action, CancellationToken ct = default) =>
        PostAsync(new WorldActionCommand(action), ct);

    protected override ValueTask OnMessageAsync(IWorldCommand command, Ticks now, CancellationToken ct)
    {
        switch (command)
        {
            case WorldRegisterChannel register:
                lock (_channels)
                {
                    _channels[register.Channel.Id] = register.Channel;
                }
                break;
            case WorldRemoveChannel remove:
                lock (_channels)
                {
                    _channels.Remove(remove.ChannelId);
                }
                break;
            case WorldActionCommand action:
                _deferred.Add(action.Action);
                break;
        }

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask OnTickCoreAsync(Ticks now, CancellationToken ct)
    {
        if (_deferred.Count == 0)
            return;

        foreach (var action in _deferred)
        {
            await action(now, ct);
        }
        _deferred.Clear();
    }
}

public interface IChannelCommand;

public sealed record ChannelRegisterRoom(RoomId RoomId, string Descriptor) : IChannelCommand;
public sealed record ChannelRemoveRoom(RoomId RoomId) : IChannelCommand;
public sealed record ChannelActionCommand(Func<Ticks, CancellationToken, ValueTask> Action) : IChannelCommand;

public sealed class ChannelActor : TickActor<IChannelCommand>
{
    private readonly Dictionary<RoomId, string> _rooms = new();
    private readonly List<Func<Ticks, CancellationToken, ValueTask>> _actions = new();

    public ChannelActor(WorldActor world, ChannelId id, string name, int mailboxCapacity = 1024)
        : base($"Channel[{id}:{name}]", mailboxCapacity)
    {
        World = world;
        Id = id;
        DisplayName = name;
    }

    public WorldActor World { get; }
    public ChannelId Id { get; }
    public string DisplayName { get; }

    public ValueTask RegisterRoomAsync(RoomId roomId, string descriptor, CancellationToken ct = default) =>
        PostAsync(new ChannelRegisterRoom(roomId, descriptor), ct);

    public ValueTask RemoveRoomAsync(RoomId roomId, CancellationToken ct = default) =>
        PostAsync(new ChannelRemoveRoom(roomId), ct);

    public ValueTask ScheduleAsync(Func<Ticks, CancellationToken, ValueTask> action, CancellationToken ct = default) =>
        PostAsync(new ChannelActionCommand(action), ct);

    protected override ValueTask OnMessageAsync(IChannelCommand command, Ticks now, CancellationToken ct)
    {
        switch (command)
        {
            case ChannelRegisterRoom register:
                _rooms[register.RoomId] = register.Descriptor;
                break;
            case ChannelRemoveRoom remove:
                _rooms.Remove(remove.RoomId);
                break;
            case ChannelActionCommand action:
                _actions.Add(action.Action);
                break;
        }

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask OnTickCoreAsync(Ticks now, CancellationToken ct)
    {
        if (_actions.Count == 0)
            return;

        foreach (var action in _actions)
        {
            await action(now, ct);
        }
        _actions.Clear();
    }
}
