using System.Net;
using System.Net.Sockets;

namespace FliegenPilz.Net;

public class NetListener
{
    private TcpListener _listener;
    private HandshakeGenerator _handshakeGenerator;

    public NetListener(TcpListener listener, HandshakeGenerator handshakeGenerator)
    {
        _listener = listener;
        _handshakeGenerator = handshakeGenerator;
    }

    public NetListener(IPAddress addr, int port, HandshakeGenerator handshakeGenerator)
    {
        var listener = new TcpListener(addr, port);
        listener.Start();
        
        _listener = listener;
        _handshakeGenerator = handshakeGenerator;
    }
    
    public async Task<NetClient> AcceptAsync(CancellationToken ct)
    {
        
        
        
        var tcpClient = await _listener.AcceptTcpClientAsync(ct);

        try
        {
            return  await NetClient.AcceptServerAsync(tcpClient, _handshakeGenerator.GenerateHandshake(), ct);
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }
    }
}