using System.Net;

namespace FliegenPilz.Net;

public interface INetworkConnection : IDisposable, IAsyncDisposable
{
    IPEndPoint RemoteEndPoint { get; }
    IPEndPoint LocalEndPoint { get; }

    Task<Packet> ReadPacketAsync(CancellationToken cancellationToken);
    ValueTask WritePacketAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken);
}
