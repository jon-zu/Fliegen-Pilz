using FliegenPilz.Net;
using FliegenPilz.Util;

namespace FliegenPilz.World.Sessions;

/// <summary>
/// Convenience base class for gameplay sessions; override the virtual hooks to react to map ticks and backpressure.
/// </summary>
public abstract class GameSessionBase : IGameSession
{
    public virtual void HandlePacket(PacketReader reader)
    {
    }

    public virtual void OnTick(Ticks now)
    {
    }

    public virtual void OnTickEnd(Ticks now)
    {
    }

    /// <summary>
    /// Called when outbound backpressure is detected (session failed to enqueue packets during the last tick).
    /// Override to shed load, queue state changes, or transition to a closing state.
    /// </summary>
    public virtual void OnSlowConsumer(Ticks now)
    {
    }

    /// <summary>
    /// Invoked after the session successfully enqueues an outbound packet.
    /// </summary>
    public virtual void OnSendSucceeded()
    {
    }
}
