using System.Net;
using FliegenPilz.Crypto;
using FliegenPilz.Net;
using Microsoft.Extensions.Logging;

namespace FliegenPilz;


public class ServerConfig
{
    public IPAddress ListenAddress { get; set; } = IPAddress.Any;
    public int LoginPort { get; set; } = 8484;

    public int ChannelPortStart { get; set; } = 8485;
    public int Channels { get; set; } = 2;
}

public class Server
{
    private readonly ServerConfig _config;
    private readonly RpcServer<LoginServer, LoginHandler> _loginServer;
    
    private readonly List<NetListener> _channelListeners = [];
    private ILogger<Server> _logger;
    
    public Server(ILoggerFactory loggerFactory, ServerConfig config, HandshakeGenerator handshakeGenerator)
    {
        _logger = loggerFactory.CreateLogger<Server>();
        
        _config = config;
        var loginListener = new NetListener(_config.ListenAddress, _config.LoginPort, handshakeGenerator);
        _loginServer = new RpcServer<LoginServer, LoginHandler>(loginListener, new LoginServer(loggerFactory));

        for (var i = 0; i < _config.Channels; i++)
        {
            var port = (short)(_config.ChannelPortStart + i);
            var channelListener = new NetListener(_config.ListenAddress, port, handshakeGenerator);
            _channelListeners.Add(channelListener);
        }
    }

    private async Task RunChannelServer(NetListener listener, CancellationToken ct)
    {
        _logger.LogInformation("Starting channel server");
        while (true)
        {
            await using var client = await listener.AcceptAsync(ct);
            _logger.LogInformation("New client connected");
        }

        return;
    }


    public async Task RunAsync(CancellationToken ct)
    {
        var taskList = new List<Task> { _loginServer.Run(ct) };
        taskList.AddRange(_channelListeners.Select(c => RunChannelServer(c, ct)));

        await Task.WhenAll(taskList);
    }
}