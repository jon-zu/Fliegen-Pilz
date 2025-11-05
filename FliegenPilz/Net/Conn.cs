using System.Threading.Channels;
using System.Threading.Tasks;

namespace FliegenPilz.Net;

/// <summary>
/// Bidirectional connection pump that bridges a <see cref="NetClient"/> with two channels:
/// one for inbound packets (produced) and one for outbound packets (consumed).
/// </summary>
/// <remarks>
/// Ownership of packet instances is transferred to the receiving side. Outbound packets are disposed
/// immediately after writing. Inbound packets must be disposed by the consumer reading from the channel.
/// </remarks>
public sealed class Conn
{
    private readonly INetworkConnection _client;
    private readonly ChannelWriter<Packet> _inboundWriter; // producer writes received packets here
    private readonly ChannelReader<Packet> _outboundReader; // consumer provides packets to send
    private readonly CancellationToken _ct;

    public Conn(INetworkConnection client, ChannelWriter<Packet> inboundWriter, ChannelReader<Packet> outboundReader, CancellationToken ct)
    {
        _client = client;
        _inboundWriter = inboundWriter;
        _outboundReader = outboundReader;
        _ct = ct;
    }

    private async Task ReceiveLoop()
    {
        while (!_ct.IsCancellationRequested)
        {
            Packet packet;
            try
            {
                packet = await _client.ReadPacketAsync(_ct);
            }
            catch (OperationCanceledException) when (_ct.IsCancellationRequested)
            {
                break;
            }
            await _inboundWriter.WriteAsync(packet, _ct);
        }
    }

    private async Task SendLoop()
    {
        while (!_ct.IsCancellationRequested)
        {
            Packet packet;
            try
            {
                packet = await _outboundReader.ReadAsync(_ct);
            }
            catch (OperationCanceledException) when (_ct.IsCancellationRequested)
            {
                break;
            }
            using (packet)
            {
                await _client.WritePacketAsync(packet.Span, _ct);
            }
        }
    }

    /// <summary>Runs both send and receive loops until cancellation.</summary>
    public async Task RunAsync()
    {
        var recv = ReceiveLoop();
        var send = SendLoop();
        try
        {
            await Task.WhenAll(recv, send);
        }
        catch (OperationCanceledException) when (_ct.IsCancellationRequested)
        {
            // normal shutdown
        }
        finally
        {
            await _client.DisposeAsync();
        }
    }
}

/// <summary>
/// Handle returned to the caller encapsulating channel endpoints and cooperative cancellation
/// for a running <see cref="Conn"/>.
/// </summary>
public sealed class ConnHandle : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _connTask;
    private readonly ChannelWriter<Packet> _outboundWriter;
    private readonly INetworkConnection _connection;

    private ConnHandle(INetworkConnection connection, Task connTask, ChannelWriter<Packet> outboundWriter, ChannelReader<Packet> inboundReader, CancellationTokenSource cts)
    {
        _cts = cts;
        _outboundWriter = outboundWriter;
        Reader = inboundReader;
        _connTask = connTask;
        _connection = connection;
    }

    /// <summary>Starts a connection pump for the given <see cref="NetClient"/> with bounded channel capacity.</summary>
    public static ConnHandle Run(INetworkConnection client, int capacity)
    {
        var inbound = Channel.CreateBounded<Packet>(capacity);   // packets received from network -> consumer reads
        var outbound = Channel.CreateBounded<Packet>(capacity);  // packets to send -> producer writes
        var cts = new CancellationTokenSource();
        var conn = new Conn(client, inbound.Writer, outbound.Reader, cts.Token);
        var task = conn.RunAsync();
        return new ConnHandle(client, task, outbound.Writer, inbound.Reader, cts);
    }

    /// <summary>Inbound packets from remote peer (caller must dispose after processing).</summary>
    public ChannelReader<Packet> Reader { get; }

    /// <summary>Attempts a non-blocking enqueue of an outbound packet (ownership transfers on success).</summary>
    public bool TrySend(Packet packet) => _outboundWriter.TryWrite(packet);

    /// <summary>Asynchronously enqueues an outbound packet (ownership transfers to connection).</summary>
    public ValueTask SendAsync(Packet packet, CancellationToken ct = default) => _outboundWriter.WriteAsync(packet, ct);

    /// <summary>Completion task signaled when the underlying connection loops exit.</summary>
    public Task Completion => _connTask;

    /// <summary>Await connection completion with optional cancellation.</summary>
    public ValueTask WaitAsync(CancellationToken ct = default) =>
        ct.CanBeCanceled ? new ValueTask(_connTask.WaitAsync(ct)) : new ValueTask(_connTask);

    /// <summary>Requests shutdown and waits (best-effort) for the connection loop to stop.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        try
        {
            _connTask.GetAwaiter().GetResult();
        }
        catch
        {
            // ignore
        }
    }
}
