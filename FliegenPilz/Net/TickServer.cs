using System.Diagnostics;
using System.Threading.Channels;

namespace FliegenPilz.Net;


public readonly record struct Tick(int Value)
{
    public static Tick operator +(Tick t, int delta) => new(t.Value + delta);
    public static Tick operator -(Tick t, int delta) => new(t.Value - delta);
    public static int operator -(Tick a, Tick b) => a.Value - b.Value;
    public static bool operator >(Tick a, Tick b) => a.Value > b.Value;
    public static bool operator <(Tick a, Tick b) => a.Value < b.Value;
    public static bool operator >=(Tick a, Tick b) => a.Value >= b.Value;
    public static bool operator <=(Tick a, Tick b) => a.Value <= b.Value;
}

public class TickClock(double tickRateHz)
{
    private readonly long _tickIntervalTicks = (long)(Stopwatch.Frequency / tickRateHz);
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();

    public Tick GetCurrentTick()
    {
        var now = Stopwatch.GetTimestamp();
        var ticksElapsed = (now - _startTimestamp) / _tickIntervalTicks;
        return new Tick((int)ticksElapsed);
    }

    public long GetTimestampForTick(Tick tick)
    {
        return _startTimestamp + tick.Value * _tickIntervalTicks;
    }

    public int MillisecondsUntilNextTick()
    {
        var next = GetCurrentTick() + 1;
        return MillisecondsUntilTick(next);
    }

    public int MillisecondsUntilTick(Tick tick)
    {
        var now = Stopwatch.GetTimestamp();
        var target = GetTimestampForTick(tick);
        var deltaTicks = target - now;
        if (deltaTicks <= 0) return 0;
        return (int)(deltaTicks * 1000 / Stopwatch.Frequency);
    }

    public TickHandle CreateHandle()
    {
        return new TickHandle(this);
    }
}

public class TickHandle
{
    private readonly TickClock _clock;
    private Tick _nextTick;

    public TickHandle(TickClock clock)
    {
        _clock = clock;
        _nextTick = _clock.GetCurrentTick();
    }

    public async Task<Tick> WaitForNextAsync()
    {
        _nextTick += 1;
        var delayMs = _clock.MillisecondsUntilTick(_nextTick);
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }
        return _nextTick;
    }

    public Tick NextTick => _nextTick;
}

public interface ISession
{
    void HandleNetPacket(PacketReader rdr);

    void HandleTick(Tick tick);
}

public class Session<T> where T : ISession
{
    private ConnHandle _conn;
    private T _session;
    //private ChannelReader<TMsg> _reader;

    private int _sessionId;
    
    public int SessionId => _sessionId;
    //TODO event queue based on heap 

    public void HandleTick(Tick tick)
    {
        // Handle net packets
        while (_conn.Reader.TryRead(out var pkt))
        {
            //TODO dispose packet
            var reader = new PacketReader(pkt);
            _session.HandleNetPacket(reader);
        }
        
        // Handle messages
        /*while (_reader.TryRead(out var msg))
        {
            _session.HandleMessage(msg);
        }*/
        
        _session.HandleTick(tick);
    }
}


public interface IRoomControlMsg
{
}

public record RoomAddSessionMsg<TSession>(Session<TSession> Session) : IRoomControlMsg where TSession : ISession
{
}

public record RoomRemoveSessionMsg(int SessionId) : IRoomControlMsg
{
}

public interface IRoom<TSession> where TSession: ISession
{
    void HandleTick(Tick tick);
    
    
    void AddSession(Session<TSession> session);
    void RemoveSession(Session<TSession> session);
}

public class Room<TRoom, TSession, TMsg, TEvent> where TRoom : IRoom<TSession>
    where TSession : ISession
{
    private List<Session<TSession>> _sessions;
    private ChannelReader<IRoomControlMsg> _reader;
    private ChannelWriter<IRoomControlMsg> _writer;
    private TRoom _room;
    private Tick? _idleSince = null;
    private TickHandle _tickHandle;


    public bool CheckShutdown(Tick tick)
    {
        if (_sessions.Count != 0)
        {
            _idleSince = null;
            return false;
        }

        if (_idleSince == null)
        {
            _idleSince = tick;
            return false;
        }

        return (tick - _idleSince.Value) > 100;

    }
    
    public void HandleTick(Tick tick)
    {
        while (_reader.TryRead(out var msg))
        {
            switch (msg) 
            {
                case RoomAddSessionMsg<TSession> addSession:
                    var session = addSession.Session;
                    _sessions.Add(session);
                    _room.AddSession(session);
                    break;
                case RoomRemoveSessionMsg removeSession:
                    var sessionToRemove = _sessions.FindIndex(s => s.SessionId == removeSession.SessionId);
                    if (sessionToRemove != -1)
                    {
                        _room.RemoveSession(_sessions[sessionToRemove]);
                        _sessions.RemoveAt(sessionToRemove);
                    }
                    break;
            }
        }
        
        
        _room.HandleTick(tick);

        foreach (var session in _sessions)
        {
            session.HandleTick(tick);
        }
    }

    public async Task Run()
    {
        while (true)
        {

            var tick = await _tickHandle.WaitForNextAsync();
            HandleTick(tick);

            if (CheckShutdown(tick))
            {
                //TODO
                Shutdown();
                break;
            }
        }
    }


    private void Shutdown()
    {
        
    }
} 




public class TickServer
{
    
}