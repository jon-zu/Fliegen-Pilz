using System.Net;
using System.Threading;
using FliegenPilz.Data;
using FliegenPilz.Net;
using FliegenPilz.Util;
using FliegenPilz.World.Sessions;
using Microsoft.Extensions.Logging;

namespace FliegenPilz.World;

/// <summary>Simple gameplay session that reacts to backpressure by scheduling removal if the client stays slow.</summary>
public sealed class PlayerSession : GameSessionBase
{
    private readonly int _sessionId;
    private readonly RoomRuntime<PlayerSession> _roomRuntime;
    private readonly ILogger<PlayerSession> _logger;
    private readonly Action<PlayerSession> _onClosed;

    private bool _awaitingRecovery;
    private int _closed;
    private CharacterEntity? _character;
    private bool _initialTickLogged;

    public PlayerSession(int sessionId, RoomRuntime<PlayerSession> roomRuntime, ILogger<PlayerSession> logger, Action<PlayerSession> onClosed)
    {
        _sessionId = sessionId;
        _roomRuntime = roomRuntime;
        _logger = logger;
        _onClosed = onClosed;
    }

    public int SessionId => _sessionId;
    public AccountId AccountId { get; set; }
    public CharacterId CharacterId { get; set; }
    public IPEndPoint? RemoteEndPoint { get; set; }
    public ulong ClientSessionId { get; set; }

    public void Initialize(CharacterEntity entity)
    {
        _character = entity;
        _initialTickLogged = false;
        _logger.LogInformation("Session {SessionId} loaded character {Name} (Lv{Level})", _sessionId, entity.Name, entity.Level);
    }

    public override void HandlePacket(PacketReader reader)
    {
        if (_character is null)
        {
            _logger.LogWarning("Session {SessionId} received packet before character initialization", _sessionId);
            return;
        }

        if (reader.Remaining < sizeof(short))
        {
            _logger.LogWarning("Session {SessionId} received truncated packet", _sessionId);
            return;
        }

        var opcode = reader.ReadShort();
        _logger.LogTrace("Session {SessionId} received opcode 0x{Opcode:X4} ({Remaining} bytes)", _sessionId, opcode, reader.Remaining);
        // TODO: dispatch packet handlers
    }

    public override void OnTick(Ticks now)
    {
        if (_character is null)
            return;

        if (!_initialTickLogged)
        {
            _logger.LogInformation("Session {SessionId} entered room {MapId} at tick {Tick}", _sessionId, _character.MapId, now.Milliseconds);
            _initialTickLogged = true;
        }

        // TODO: heartbeat and queued event processing
    }

    public override void OnSlowConsumer(Ticks now)
    {
        if (_awaitingRecovery)
        {
            _logger.LogWarning("Session {SessionId} (Account {AccountId}) still overloaded at tick {Tick}", _sessionId, AccountId.Value, now.Milliseconds);
            return;
        }

        _awaitingRecovery = true;
        _logger.LogWarning("Session {SessionId} (Account {AccountId}) flagged as slow at tick {Tick}", _sessionId, AccountId.Value, now.Milliseconds);

        _roomRuntime.ScheduleAfterMilliseconds(3_000, async (tick, ct) =>
        {
            if (!_awaitingRecovery)
                return;

            _logger.LogWarning("Session {SessionId} (Account {AccountId}) did not recover; removing from room at tick {Tick}", _sessionId, AccountId.Value, tick.Milliseconds);
            await _roomRuntime.Executor.PostAsync(new RemoveSessionCommand<PlayerSession>(_sessionId), ct);
        });
    }

    public override void OnSendSucceeded()
    {
        if (_awaitingRecovery)
        {
            _awaitingRecovery = false;
            _logger.LogInformation("Session {SessionId} (Account {AccountId}) recovered from slow state", _sessionId, AccountId.Value);
        }
    }

    public void NotifyClosed()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
            return;
        _onClosed(this);
    }
}
