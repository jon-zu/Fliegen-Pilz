// See https://aka.ms/new-console-template for more information

using FliegenPilz;
using FliegenPilz.Crypto;
using FliegenPilz.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Services
        services.AddSingleton<Server>();
        services.AddSingleton<HandshakeGenerator>(new HandshakeGenerator(new ShroomVersion(95), "1", 2));
    })
    .Build();

// Start server
var server = host.Services.GetRequiredService<Server>();
await server.StartAsync(CancellationToken.None);