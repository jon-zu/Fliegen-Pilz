using System;
using System.Buffers.Binary;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Act;
using FliegenPilz.Data;
using FliegenPilz.Net;
using FliegenPilz.Util;
using FliegenPilz.World;
using FliegenPilz.World.Sessions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FliegenPilz.Tests.World;

public class SessionConnectionHandlerTests
{
    [Fact]
    public async Task SuccessfulMigrationRegistersAndRemovesSession()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<FliegenPilzDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var ctx = new FliegenPilzDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        var factory = new DelegateDbContextFactory(() => new FliegenPilzDbContext(options));
        var sessionManager = new SessionManager(NullLoggerFactory.Instance, factory);
        var handler = new SessionConnectionHandler(sessionManager, NullLoggerFactory.Instance);

        var notifier = new TickNotifier();
        var roomExecutor = new RoomExecutor<PlayerSession>(new RoomId(1, new MapId(1)), "TestRoom");
        var roomTimer = new RoomTimer<PlayerSession>(roomExecutor, notifier);
        var roomRuntime = (RoomRuntime<PlayerSession>)Activator.CreateInstance(
            typeof(RoomRuntime<PlayerSession>),
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new object[] { roomExecutor, roomTimer },
            null)!;

        var accountId = await sessionManager.GetOrCreateAccountAsync("Test", CancellationToken.None);
        var character = await sessionManager.EnsureDefaultCharacterAsync(accountId, CancellationToken.None);

        var (server, client) = PipeConnection.CreatePair();
        var ticket = sessionManager.CreateMigrationTicket(accountId, character.Id, server.RemoteEndPoint);

        var handshake = new byte[sizeof(ulong) + sizeof(int) * 2];
        BinaryPrimitives.WriteUInt64LittleEndian(handshake, ticket.ClientSessionId);
        BinaryPrimitives.WriteInt32LittleEndian(handshake.AsSpan(8), accountId.Value);
        BinaryPrimitives.WriteInt32LittleEndian(handshake.AsSpan(12), character.Id.Value);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var sessionTask = handler.HandleClientAsync(server, roomRuntime, cts.Token);

        await client.WritePacketAsync(handshake, CancellationToken.None);

        await SpinWaitAsync(() => sessionManager.ActiveSessionIds.Count == 1, cts.Token);

        await client.DisposeAsync();
        await sessionTask;

        Assert.Empty(sessionManager.ActiveSessionIds);

        roomTimer.Dispose();
        notifier.Dispose();
    }

    private static async Task SpinWaitAsync(Func<bool> condition, CancellationToken ct)
    {
        const int delayMs = 10;
        var elapsed = 0;
        while (!condition())
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(delayMs, ct);
            elapsed += delayMs;
            if (elapsed > 2000)
                throw new TimeoutException("Condition not met in allotted time.");
        }
    }

    private sealed class DelegateDbContextFactory(Func<FliegenPilzDbContext> factory)
        : IDbContextFactory<FliegenPilzDbContext>
    {
        public FliegenPilzDbContext CreateDbContext() => factory();

        public ValueTask<FliegenPilzDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(factory());
    }
}
