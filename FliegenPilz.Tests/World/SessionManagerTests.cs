using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Data;
using FliegenPilz.World;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FliegenPilz.Tests.World;

public class SessionManagerTests
{
    [Fact]
    public async Task CreateGuestAccountAsync_PersistsAccount()
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
        var manager = new SessionManager(NullLoggerFactory.Instance, factory);

        var accountId = await manager.CreateGuestAccountAsync(CancellationToken.None);

        await using var verifyCtx = factory.CreateDbContext();
        var account = await verifyCtx.Accounts.SingleAsync(a => a.Id == accountId);

        Assert.Equal($"Player{accountId.Value}", account.Username);
        Assert.True(account.CreatedAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateMigrationTicket_ValidatesAndConsumes()
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
        var manager = new SessionManager(NullLoggerFactory.Instance, factory);

        var accountId = await manager.CreateGuestAccountAsync(CancellationToken.None);
        var character = await manager.EnsureDefaultCharacterAsync(accountId, CancellationToken.None);

        var ticket = manager.CreateMigrationTicket(accountId, character.Id, new IPEndPoint(IPAddress.Loopback, 0));

        Assert.True(manager.TryConsumeMigrationTicket(ticket.ClientSessionId, new IPEndPoint(IPAddress.Loopback, 1234), out var consumed));
        Assert.Equal(ticket.ClientSessionId, consumed.ClientSessionId);

        Assert.False(manager.TryConsumeMigrationTicket(ticket.ClientSessionId, new IPEndPoint(IPAddress.Loopback, 1234), out _));
    }

    private sealed class DelegateDbContextFactory(Func<FliegenPilzDbContext> factory)
        : IDbContextFactory<FliegenPilzDbContext>
    {
        public FliegenPilzDbContext CreateDbContext() => factory();

        public ValueTask<FliegenPilzDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => new(factory());
    }
}
