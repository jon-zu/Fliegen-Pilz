using FliegenPilz.Proto;
using Microsoft.Extensions.Logging;

namespace FliegenPilz.Net;


public class RpcContext(NetClient client) : IDisposable, IAsyncDisposable
{
    public NetClient Client { get; } = client;


    public ValueTask SendAsync(ReadOnlySpan<byte> data, CancellationToken ct)
    {
        return Client.WritePacketAsync(data, ct);
    }

    public ValueTask ReplyAsync<T>(T data, CancellationToken ct) where T : IPacketMessage
    {
        var writer = new PacketWriter();
        writer.WriteShort((short)T.Opcode);
        data.EncodePacket(ref writer);
        using var pkt = writer.ToPacket();

        return SendAsync(pkt.Span, ct);
    }


    public void Dispose()
    {
        Client.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
    }
}

public interface IRpcHandler
{
    Task HandlePacket(PacketReader reader, RpcContext ctx, CancellationToken ct);

    void HandleException(Exception e);
}

public class RpcClient<TH>(NetClient client, TH handler) : IDisposable, IAsyncDisposable
    where TH : IRpcHandler
{
    private readonly RpcContext _ctx = new(client);
    private readonly TH _handler = handler;


    public async Task Run(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var pkt = await _ctx.Client.ReadPacketAsync(ct);
            var pr = pkt.AsReader();
            await _handler.HandlePacket(pr, _ctx, ct);
        }
    }

    public void HandleException(Exception e)
    {
        _handler.HandleException(e);
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }
}

public interface IRpcServerHandler<T>
    where T : IRpcHandler
{
    Task<RpcClient<T>> AcceptClientAsync(NetClient client, CancellationToken ct);
}

public class RpcServer<T, TH>(NetListener listener, T handler)
    where T : IRpcServerHandler<TH>
    where TH : IRpcHandler

{
    private List<RpcClient<TH>> _clients = [];
    private T _handler = handler;

    private void AddClient(RpcClient<TH> client)
    {
        var task = Task.Run(async () =>
        {
            try
            {
                await client.Run(CancellationToken.None);
            }
            catch (Exception e)
            {
                client.HandleException(e);
                await client.DisposeAsync();
                // Handle exception
            }
        });
    }
    

    public async Task Run(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var netClient = await listener.AcceptAsync(ct);
            try
            {
                
                var rpcClient = await _handler.AcceptClientAsync(netClient, ct);
                AddClient(rpcClient);
            }
            catch
            {
                await netClient.DisposeAsync();
                //TODO probably don't throw
                throw;
            }
        }
    }
}