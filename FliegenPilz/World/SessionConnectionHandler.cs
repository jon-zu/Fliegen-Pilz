using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Data;
using FliegenPilz.Net;
using FliegenPilz.World.Sessions;
using Microsoft.Extensions.Logging;

namespace FliegenPilz.World;

/// <summary>
/// Handles the connection lifecycle for clients migrating into a map, including migration ticket validation and session creation.
/// </summary>
public sealed class SessionConnectionHandler
{
    private readonly SessionManager _sessionManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SessionConnectionHandler> _logger;

    private int _nextSessionId;

    public SessionConnectionHandler(SessionManager sessionManager, ILoggerFactory loggerFactory)
    {
        _sessionManager = sessionManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<SessionConnectionHandler>();
    }

    public async Task HandleClientAsync(INetworkConnection client, RoomRuntime<PlayerSession> roomRuntime, CancellationToken ct)
    {
        await using var ownedClient = client;
        using var connection = ConnHandle.Run(client, capacity: 128);

        var sessionId = Interlocked.Increment(ref _nextSessionId);

        var context = await TryAuthenticateAsync(sessionId, client, connection, roomRuntime, ct);
        if (context is null)
            return;
        var sessionContext = context.Value;

        try
        {
            await connection.WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // server shutdown path
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session {SessionId} terminated with error", sessionId);
        }
        finally
        {
            await roomRuntime.Executor.PostAsync(new RemoveSessionCommand<PlayerSession>(sessionId), CancellationToken.None);
            sessionContext.Session.Dispose();
            sessionContext.PlayerSession.NotifyClosed();
            _logger.LogInformation("Session {SessionId} removed from room {Room}", sessionId, roomRuntime.Executor.RoomName);
        }
    }

    private async Task<SessionContext?> TryAuthenticateAsync(int sessionId, INetworkConnection client, ConnHandle connection, RoomRuntime<PlayerSession> roomRuntime, CancellationToken ct)
    {
        using var handshakePacket = await connection.Reader.ReadAsync(ct);
        var reader = handshakePacket.AsReader();

        if (reader.Remaining < sizeof(ulong) + sizeof(int) * 2)
        {
            _logger.LogWarning("Session {SessionId}: handshake packet too small", sessionId);
            return null;
        }

        var clientSessionId = reader.ReadULong();
        var accountId = new AccountId(reader.ReadInt());
        var characterId = new CharacterId(reader.ReadInt());

        var remoteEndPoint = client.RemoteEndPoint ?? new IPEndPoint(IPAddress.None, 0);

        if (!_sessionManager.TryConsumeMigrationTicket(clientSessionId, remoteEndPoint, out var ticket))
        {
            _logger.LogWarning("Session {SessionId}: invalid migration ticket {Ticket}", sessionId, clientSessionId);
            return null;
        }

        if (ticket.AccountId != accountId || ticket.CharacterId != characterId)
        {
            _logger.LogWarning(
                "Session {SessionId}: migration ticket mismatch (account {AccountId}/{TicketAccount}, character {CharacterId}/{TicketCharacter})",
                sessionId, accountId.Value, ticket.AccountId.Value, characterId.Value, ticket.CharacterId.Value);
            return null;
        }

        var characterEntity = await _sessionManager.LoadCharacterAsync(characterId, ct);
        if (characterEntity is null)
        {
            _logger.LogWarning("Session {SessionId}: character {CharacterId} not found", sessionId, characterId.Value);
            return null;
        }

        var playerSession = _sessionManager.CreatePlayerSession(sessionId, roomRuntime, accountId, characterId);
        playerSession.RemoteEndPoint = remoteEndPoint;
        playerSession.ClientSessionId = clientSessionId;
        playerSession.Initialize(characterEntity);

        var session = new Session<PlayerSession>(sessionId, connection, playerSession);

        await roomRuntime.Executor.PostAsync(new AddSessionCommand<PlayerSession>(session), ct);
        _logger.LogInformation("Session {SessionId} registered for account {AccountId}", sessionId, accountId.Value);

        return new SessionContext(playerSession, session);
    }

    private readonly record struct SessionContext(PlayerSession PlayerSession, Session<PlayerSession> Session);
}
