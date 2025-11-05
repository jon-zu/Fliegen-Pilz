using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using FliegenPilz.Data;
using FliegenPilz.World.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FliegenPilz.World;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<int, PlayerSession> _sessions = new();
    private readonly ConcurrentDictionary<ulong, MigrationTicket> _pendingTickets = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly IDbContextFactory<FliegenPilzDbContext> _dbContextFactory;
    private readonly ILogger<SessionManager> _logger;

    private int _nextAccountId;

    public SessionManager(ILoggerFactory loggerFactory, IDbContextFactory<FliegenPilzDbContext> dbContextFactory)
    {
        _loggerFactory = loggerFactory;
        _dbContextFactory = dbContextFactory;
        _logger = loggerFactory.CreateLogger<SessionManager>();
    }

    public IReadOnlyCollection<int> ActiveSessionIds => _sessions.Keys.ToArray();
    public IReadOnlyCollection<PlayerSession> ActiveSessions => _sessions.Values.ToArray();

    public bool TryGetSession(int sessionId, out PlayerSession session) => _sessions.TryGetValue(sessionId, out session!);

    public PlayerSession CreatePlayerSession(int sessionId, RoomRuntime<PlayerSession> roomRuntime, AccountId accountId, CharacterId characterId)
    {
        var sessionLogger = _loggerFactory.CreateLogger<PlayerSession>();
        var playerSession = new PlayerSession(sessionId, roomRuntime, sessionLogger, ps => OnSessionClosed(ps.SessionId))
        {
            AccountId = accountId,
            CharacterId = characterId
        };
        if (!_sessions.TryAdd(sessionId, playerSession))
        {
            throw new InvalidOperationException($"Duplicate session id {sessionId}");
        }
        _logger.LogInformation("Session {SessionId} registered", sessionId);
        return playerSession;
    }

    public async Task<AccountId> CreateGuestAccountAsync(CancellationToken ct)
    {
        var newId = Interlocked.Increment(ref _nextAccountId);
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.Accounts.FirstOrDefaultAsync(a => a.Username == $"Player{newId}", ct);
        if (existing != null) return existing.Id;

        var account = new AccountEntity
        {
            Username = $"Player{newId}",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    public async Task<CharacterEntity?> LoadCharacterAsync(CharacterId characterId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == characterId, ct);
    }

    public async Task<AccountId> GetOrCreateAccountAsync(string username, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await db.Accounts.FirstOrDefaultAsync(a => a.Username == username, ct);
        if (existing != null) return existing.Id;

        var account = new AccountEntity
        {
            Username = username,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    public async Task<CharacterEntity> EnsureDefaultCharacterAsync(AccountId accountId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var character = await db.Characters.FirstOrDefaultAsync(c => c.AccountId == accountId, ct);
        if (character != null) return character;

        character = new CharacterEntity
        {
            AccountId = accountId,
            Name = $"Hero{accountId.Value}",
            Level = 1,
            MapId = 100000000,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync(ct);
        return character;
    }

    public async Task<List<CharacterEntity>> GetCharactersAsync(AccountId accountId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.Characters.Where(c => c.AccountId == accountId).ToListAsync(ct);
    }

    public MigrationTicket CreateMigrationTicket(AccountId accountId, CharacterId characterId, IPEndPoint endpoint, TimeSpan? ttl = null)
    {
        CleanupExpiredTickets();
        var ticket = new MigrationTicket(NextClientSessionId(), accountId, characterId, endpoint, DateTime.UtcNow + (ttl ?? TimeSpan.FromSeconds(30)));
        _pendingTickets[ticket.ClientSessionId] = ticket;
        return ticket;
    }

    public bool TryConsumeMigrationTicket(ulong clientSessionId, IPEndPoint endpoint, out MigrationTicket ticket)
    {
        CleanupExpiredTickets();
        if (_pendingTickets.TryRemove(clientSessionId, out ticket))
        {
            if (ticket.ExpiresAtUtc < DateTime.UtcNow)
            {
                ticket = default;
                return false;
            }

            if (!ticket.RemoteEndPoint.Address.Equals(endpoint.Address))
            {
                _logger.LogWarning("Migration ticket {Ticket} rejected due to IP mismatch {TicketIp} vs {ClientIp}",
                    clientSessionId, ticket.RemoteEndPoint.Address, endpoint.Address);
                ticket = default;
                return false;
            }
            return true;
        }

        ticket = default;
        return false;
    }

    private void CleanupExpiredTickets()
    {
        var now = DateTime.UtcNow;
        foreach (var (key, ticket) in _pendingTickets)
        {
            if (ticket.ExpiresAtUtc < now)
            {
                _pendingTickets.TryRemove(key, out _);
            }
        }
    }

    private static ulong NextClientSessionId()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt64(buffer);
        return value == 0 ? 1UL : value;
    }

    public readonly record struct MigrationTicket(ulong ClientSessionId, AccountId AccountId, CharacterId CharacterId, IPEndPoint RemoteEndPoint, DateTime ExpiresAtUtc);

    private void OnSessionClosed(int sessionId)
    {
        if (_sessions.TryRemove(sessionId, out _))
        {
            _logger.LogInformation("Session {SessionId} deregistered", sessionId);
        }
    }
}
