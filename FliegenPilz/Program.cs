using System.Net;
using FliegenPilz;
using FliegenPilz.Crypto;
using FliegenPilz.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddSingleton<Server>();
    })
    .Build();

// Start server
var server = host.Services.GetRequiredService<Server>();
await server.RunAsync(CancellationToken.None);