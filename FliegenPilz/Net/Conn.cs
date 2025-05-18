using System.Threading.Channels;

namespace FliegenPilz.Net;

public class Conn
{
    private NetClient _client;
    private ChannelWriter<Packet> _txIn;
    private ChannelReader<Packet> _rxOut;
    private CancellationToken _ct;


    public Conn(NetClient client, ChannelWriter<Packet> txIn, ChannelReader<Packet> rxOut, CancellationToken ct)
    {
        _client = client;
        _rxOut = rxOut;
        _txIn = txIn;
        _ct = ct;
    }
    
    private async Task RecvTask()
    {
        while (!_ct.IsCancellationRequested)
        {
            var packet = await _client.ReadPacketAsync(_ct);
            try
            {
                await _txIn.WriteAsync(packet, _ct);
            }
            catch
            {
                packet.Dispose();
                throw;
            }
        }
    }

    private async Task SendTask()
    {
        while (!_ct.IsCancellationRequested)
        {
            using var packet = await _rxOut.ReadAsync(_ct);
            await _client.WritePacketAsync(packet.Span, _ct);
        }
    }
    
    public async Task Run()
    {
        var recvTask = RecvTask();
        var sendTask = SendTask();
        try
        {
            await Task.WhenAll(recvTask, sendTask);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception e)
        {
            // Handle other exceptions
            throw;
        }
    }
}


public class ConnHandle : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _connTask;
    private readonly ChannelWriter<Packet> _txOut;

    ConnHandle(NetClient client, Task connTask, ChannelWriter<Packet> txOut, ChannelReader<Packet> rxIn, CancellationTokenSource cts)
    {
        _cts = cts;
        _txOut = txOut;
        Reader = rxIn;
        _connTask = connTask;
    }

    public static ConnHandle Run(NetClient client, int cap)
    {
        var outChannel = Channel.CreateBounded<Packet>(cap);
        var inChannel = Channel.CreateBounded<Packet>(cap);
        var cts = new CancellationTokenSource();
        var conn = new Conn(client, outChannel.Writer, inChannel.Reader, cts.Token);
        var connTask = conn.Run();
        return new ConnHandle(client, connTask, outChannel.Writer, inChannel.Reader, cts);
    }


    public ChannelReader<Packet> Reader { get; }

    public bool TrySend(Packet packet)
    {
        return _txOut.TryWrite(packet);
    }
    
    public async Task SendAsync(Packet packet)
    {
        await _txOut.WriteAsync(packet);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _connTask.Dispose();
    }
}