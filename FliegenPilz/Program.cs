using System.Net;
using FliegenPilz;
using FliegenPilz.Act;
using FliegenPilz.Crypto;
using FliegenPilz.Data;
using FliegenPilz.Net;
using FliegenPilz.Util;
using FliegenPilz.World;
using FliegenPilz.World.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Services
        services.AddSingleton(new HandshakeGenerator(new ShroomVersion(95), "1", LocaleCode.Global));
        services.AddSingleton(new ServerConfig
        {
            ListenAddress = IPAddress.Any,
            LoginPort = 8484
        });
        services.Configure<TickSchedulerOptions>(opts =>
        {
            opts.TickInterval = TimeSpan.FromMilliseconds(50); // TODO: expose via config
        });
        var connectionString = context.Configuration.GetConnectionString("Default")
            ?? "Data Source=fliegenpilz.db";
        void ConfigureDb(DbContextOptionsBuilder opts) => opts.UseSqlite(connectionString);
        services.AddDbContext<FliegenPilzDbContext>(ConfigureDb);
        services.AddDbContextFactory<FliegenPilzDbContext>(ConfigureDb);
        services.AddSingleton<GlobalClock>();
        services.AddSingleton<TickNotifier>();
        services.AddSingleton<TickScheduler>();
        services.AddHostedService(sp => sp.GetRequiredService<TickScheduler>());
        services.AddSingleton<RoomServer>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<SessionConnectionHandler>();
        services.AddSingleton<Server>();
    })
    .Build();

// Start server
var server = host.Services.GetRequiredService<Server>();
await server.RunAsync(CancellationToken.None);
