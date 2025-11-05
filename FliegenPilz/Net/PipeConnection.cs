using System.Buffers;
using System.Net;
using System.Threading.Channels;

namespace FliegenPilz.Net;

public sealed class PipeConnection : INetworkConnection
{
    private static int _nextPort = 40000;

    private readonly ChannelReader<Packet> _inbound;
    private readonly ChannelWriter<Packet> _outbound;
    private readonly IPEndPoint _localEndPoint;
    private readonly IPEndPoint _remoteEndPoint;
    private bool _disposed;

    private PipeConnection(ChannelReader<Packet> inbound, ChannelWriter<Packet> outbound, IPEndPoint local, IPEndPoint remote)
    {
        _inbound = inbound;
        _outbound = outbound;
        _localEndPoint = local;
        _remoteEndPoint = remote;
    }

    public IPEndPoint RemoteEndPoint => _remoteEndPoint;
    public IPEndPoint LocalEndPoint => _localEndPoint;

    public static (PipeConnection server, PipeConnection client) CreatePair()
    {
        var channelOptions = new BoundedChannelOptions(64)
        {
            SingleReader = false,
            SingleWriter = false
        };

        var ab = Channel.CreateBounded<Packet>(channelOptions);
        var ba = Channel.CreateBounded<Packet>(channelOptions);

        var portA = Interlocked.Increment(ref _nextPort);
        var portB = Interlocked.Increment(ref _nextPort);

        var endpointA = new IPEndPoint(IPAddress.Loopback, portA);
        var endpointB = new IPEndPoint(IPAddress.Loopback, portB);

        var server = new PipeConnection(ab.Reader, ba.Writer, endpointA, endpointB);
        var client = new PipeConnection(ba.Reader, ab.Writer, endpointB, endpointA);
        return (server, client);
    }

    public async Task<Packet> ReadPacketAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _inbound.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    public ValueTask WritePacketAsync(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        var owner = MemoryPool<byte>.Shared.Rent(data.Length);
        var memory = owner.Memory[..data.Length];
        data.CopyTo(memory.Span);
        var packet = new Packet(owner, data.Length);

        var writeTask = _outbound.WriteAsync(packet, cancellationToken);
        if (writeTask.IsCompletedSuccessfully)
        {
            return ValueTask.CompletedTask;
        }

        return AwaitWriteAsync(writeTask, owner);

        static async ValueTask AwaitWriteAsync(ValueTask task, IMemoryOwner<byte> owner)
        {
            try
            {
                await task;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _outbound.TryComplete();
    }
}

public sealed class PipeListener : IDisposable
{
    private readonly Channel<PipeConnection> _pending = Channel.CreateUnbounded<PipeConnection>();
    private bool _disposed;

    public PipeConnection Connect()
    {
        var (server, client) = PipeConnection.CreatePair();
        _pending.Writer.TryWrite(server);
        return client;
    }

    public async Task<PipeConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        return await _pending.Reader.ReadAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pending.Writer.TryComplete();
    }
}
