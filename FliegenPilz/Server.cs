using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Crypto;
using FliegenPilz.Net;
using FliegenPilz.World;
using FliegenPilz.World.Sessions;
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
    private readonly Dictionary<NetListener, ChannelActor> _channelExecutors = new();
    private readonly Dictionary<NetListener, RoomRuntime<PlayerSession>> _channelRooms = new();
    private readonly ILogger<Server> _logger;
    private readonly RoomServer _roomServer;
    private readonly SessionManager _sessionManager;
    private readonly SessionConnectionHandler _sessionHandler;
    private readonly WorldActor _worldActor;

    public Server(ILoggerFactory loggerFactory, ServerConfig config, HandshakeGenerator handshakeGenerator, RoomServer roomServer, SessionManager sessionManager, SessionConnectionHandler sessionHandler)
    {
        _logger = loggerFactory.CreateLogger<Server>();
        _roomServer = roomServer;
        _sessionManager = sessionManager;
        _sessionHandler = sessionHandler;
        
        _config = config;
        var loginListener = new NetListener(_config.ListenAddress, _config.LoginPort, handshakeGenerator);
        _loginServer = new RpcServer<LoginServer, LoginHandler>(loginListener, new LoginServer(loggerFactory, sessionManager));

        _worldActor = _roomServer.CreateWorld(new WorldId(1), "Main");

        for (var i = 0; i < _config.Channels; i++)
        {
            var port = (short)(_config.ChannelPortStart + i);
            var channelListener = new NetListener(_config.ListenAddress, port, handshakeGenerator);
            _channelListeners.Add(channelListener);
            var channelActor = _roomServer.CreateChannel(_worldActor, new ChannelId(i + 1), $"Channel-{i + 1}");
            _channelExecutors[channelListener] = channelActor;
            var roomRuntime = _roomServer.CreateRoom<PlayerSession>(channelActor, new RoomId(i + 1, new MapId(100000000 + i)), $"Channel-{i + 1}-Room");
            _channelRooms[channelListener] = roomRuntime;
            StartRoomMaintenance(roomRuntime);
        }
    }

    private void StartRoomMaintenance(RoomRuntime<PlayerSession> roomRuntime)
    {
        void ScheduleSweep()
        {
            roomRuntime.ScheduleAfterMilliseconds(5_000, (tick, token) =>
            {
                _logger.LogDebug("[Room {Room}] maintenance at {Tick}ms", roomRuntime.Executor.RoomName, tick.Milliseconds);
                ScheduleSweep();
                return ValueTask.CompletedTask;
            });
        }

        ScheduleSweep();
    }

    private async Task RunChannelServer(NetListener listener, ChannelActor channel, RoomRuntime<PlayerSession> roomRuntime, CancellationToken ct)
    {
        _logger.LogInformation("Starting channel server {Channel}", channel.DisplayName);
        while (!ct.IsCancellationRequested)
        {
            NetClient client;
            try
            {
                client = await listener.AcceptAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept client on {Channel}", channel.DisplayName);
                continue;
            }

            _ = _sessionHandler.HandleClientAsync(client, roomRuntime, ct);
        }
    }
    public async Task RunAsync(CancellationToken ct)
    {
        var taskList = new List<Task> { _loginServer.Run(ct) };
        taskList.AddRange(_channelListeners.Select(listener =>
        {
            var channel = _channelExecutors[listener];
            var roomRuntime = _channelRooms[listener];
            return RunChannelServer(listener, channel, roomRuntime, ct);
        }));

        await Task.WhenAll(taskList);
    }
}
