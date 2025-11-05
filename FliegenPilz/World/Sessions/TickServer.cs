using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Act;
using FliegenPilz.Net;
using FliegenPilz.Util;

namespace FliegenPilz.World.Sessions;

public interface IGameSession
{
    void HandlePacket(PacketReader reader);
    void OnTick(Ticks now);
    void OnTickEnd(Ticks now);
    void OnSlowConsumer(Ticks now);
    void OnSendSucceeded();
}

public sealed class Session<TSession> : IDisposable where TSession : IGameSession
{
    private readonly ConnHandle _connection;
    private readonly TSession _logic;
    private bool _slowConsumer;

    public Session(int sessionId, ConnHandle connection, TSession logic)
    {
        SessionId = sessionId;
        _connection = connection;
        _logic = logic;
    }

    public int SessionId { get; }

    public void Tick(Ticks now)
    {
        DrainInboundPackets();
        _logic.OnTick(now);
    }

    public bool TrySend(Packet packet)
    {
        if (!_connection.TrySend(packet))
        {
            _slowConsumer = true;
            return false;
        }
        _logic.OnSendSucceeded();
        return true;
    }

    public ValueTask SendAsync(Packet packet, CancellationToken ct = default) => SendCoreAsync(packet, ct);

    private async ValueTask SendCoreAsync(Packet packet, CancellationToken ct)
    {
        await _connection.SendAsync(packet, ct).ConfigureAwait(false);
        _logic.OnSendSucceeded();
    }

    public void MarkSlowConsumer() => _slowConsumer = true;

    public void TickEnd(Ticks now)
    {
        if (_slowConsumer)
        {
            _logic.OnSlowConsumer(now);
            _slowConsumer = false;
        }
        _logic.OnTickEnd(now);
    }

    private void DrainInboundPackets()
    {
        while (_connection.Reader.TryRead(out var packet))
        {
            using (packet)
            {
                var reader = new PacketReader(packet);
                _logic.HandlePacket(reader);
            }
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

public interface IRoomCommand<TSession> where TSession : IGameSession;

public sealed record AddSessionCommand<TSession>(Session<TSession> Session)
    : IRoomCommand<TSession> where TSession : IGameSession;

public sealed record RemoveSessionCommand<TSession>(int SessionId)
    : IRoomCommand<TSession> where TSession : IGameSession;

public sealed record RoomActionCommand<TSession>(Func<Ticks, CancellationToken, ValueTask> Action)
    : IRoomCommand<TSession> where TSession : IGameSession;

public class RoomExecutor<TSession> : TickActor<IRoomCommand<TSession>> where TSession : IGameSession
{
    private readonly List<Session<TSession>> _sessions = new();
    private readonly Dictionary<int, Session<TSession>> _sessionLookup = new();

    public RoomExecutor(RoomId id, string name, int mailboxCapacity = 1024)
        : base($"Room[{id.FieldId.Value}:{id.InstanceId}:{name}]", mailboxCapacity)
    {
        Id = id;
        RoomName = name;
    }

    public RoomId Id { get; }
    public string RoomName { get; }

    protected override ValueTask OnMessageAsync(IRoomCommand<TSession> message, Ticks now, CancellationToken ct)
    {
        switch (message)
        {
            case AddSessionCommand<TSession> add:
                RegisterSession(add.Session);
                break;
            case RemoveSessionCommand<TSession> remove:
                UnregisterSession(remove.SessionId);
                break;
            case RoomActionCommand<TSession> action:
                return action.Action(now, ct);
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnTickCoreAsync(Ticks now, CancellationToken ct)
    {
        foreach (var session in _sessions)
        {
            session.Tick(now);
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnTickEndAsync(Ticks now, CancellationToken ct)
    {
        foreach (var session in _sessions)
        {
            session.TickEnd(now);
        }
        return ValueTask.CompletedTask;
    }

    private void RegisterSession(Session<TSession> session)
    {
        if (_sessionLookup.ContainsKey(session.SessionId))
            return;

        _sessions.Add(session);
        _sessionLookup[session.SessionId] = session;
    }

    private void UnregisterSession(int sessionId)
    {
        if (!_sessionLookup.TryGetValue(sessionId, out var session))
            return;

        _sessionLookup.Remove(sessionId);
        _sessions.Remove(session);
        session.Dispose();
    }
}

public sealed class RoomServer : IDisposable
{
    private readonly TickScheduler _scheduler;
    private readonly TickNotifier _notifier;
    private readonly List<ActorRegistration> _registrations = new();

    public RoomServer(TickScheduler scheduler, TickNotifier notifier)
    {
        _scheduler = scheduler;
        _notifier = notifier;
    }

    public TickNotifier Notifier => _notifier;

    public WorldActor CreateWorld(WorldId id, string name, int mailboxCapacity = 1024)
    {
        var world = new WorldActor(id, name, mailboxCapacity);
        var handle = _scheduler.Register(world);
        _registrations.Add(new ActorRegistration(handle, null));
        return world;
    }

    public ChannelActor CreateChannel(WorldActor world, ChannelId id, string name, int mailboxCapacity = 1024)
    {
        var channel = new ChannelActor(world, id, name, mailboxCapacity);
        var handle = _scheduler.Register(channel);
        _registrations.Add(new ActorRegistration(handle, () => Enqueue(world, new WorldRemoveChannel(id))));
        Enqueue(world, new WorldRegisterChannel(channel));
        return channel;
    }

    public RoomRuntime<TSession> CreateRoom<TSession>(ChannelActor channel, RoomId roomId, string descriptor, int mailboxCapacity = 1024)
        where TSession : IGameSession
    {
        var executor = new RoomExecutor<TSession>(roomId, descriptor, mailboxCapacity);
        var handle = _scheduler.Register(executor);
        var timer = new RoomTimer<TSession>(executor, _notifier);
        _registrations.Add(new ActorRegistration(handle, () =>
        {
            Enqueue(channel, new ChannelRemoveRoom(roomId));
            timer.Dispose();
        }));
        Enqueue(channel, new ChannelRegisterRoom(roomId, descriptor));
        return new RoomRuntime<TSession>(executor, timer);
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            try
            {
                registration.OnDispose?.Invoke();
            }
            catch
            {
            }

            registration.Subscription.Dispose();
        }
        _registrations.Clear();
    }

    private sealed class ActorRegistration
    {
        public ActorRegistration(IDisposable subscription, Action? onDispose)
        {
            Subscription = subscription;
            OnDispose = onDispose;
        }

        public IDisposable Subscription { get; }
        public Action? OnDispose { get; }
    }

    private static void Enqueue<TMessage>(TickActor<TMessage> actor, TMessage message)
    {
        if (!actor.TryPost(message))
        {
            actor.PostAsync(message).GetAwaiter().GetResult();
        }
    }
}
