using System.Net;
using FliegenPilz.Crypto;
using FliegenPilz.Net;
using Microsoft.Extensions.Logging;

namespace FliegenPilz;


public class ServerConfig
{
    public IPAddress ListenAddress { get; set; } = IPAddress.Any;
    public int LoginPort { get; set; } = 8484;
}

public class Server
{
    private readonly ServerConfig _config;
    private readonly RpcServer<LoginServer, LoginHandler> _loginServer;
    
    public Server(ILoggerFactory loggerFactory, ServerConfig config, HandshakeGenerator handshakeGenerator)
    {
        _config = config;
        var loginListener = new NetListener(_config.ListenAddress, _config.LoginPort, handshakeGenerator);
        _loginServer = new RpcServer<LoginServer, LoginHandler>(loginListener, new LoginServer(loggerFactory));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var loginServerTask = _loginServer.Run(ct);
        await loginServerTask;
    }
}