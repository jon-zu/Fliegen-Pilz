using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FliegenPilz.Crypto;
using FliegenPilz.Net;
using JetBrains.Annotations;
using Xunit;

namespace FliegenPilz.Tests.Net;

[TestSubject(typeof(NetCodec))]
public class NetCodecTest
{
    [Fact]
    public async Task ClientServer()
    {
        var addr = IPAddress.Loopback;
        const int port = 38120;
        var listener = new TcpListener(addr, port);
        var cts = new CancellationTokenSource();

        var tcs = new TaskCompletionSource<string>("");

        var handshake = new Handshake(new ShroomVersion(95), "1", RoundKey.GetRandom(), RoundKey.GetRandom(), 2);

        // Spawn server in new task
        var serverTask = Task.Run(async () =>
        {
            try
            {
                listener.Start();
                var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                var netClient = await NetCodec.AcceptServerAsync(tcpClient, handshake, cts.Token);


                using var pkt = await netClient.ReadPacketAsync(cts.Token);
                await netClient.WritePacketAsync(pkt.Span, cts.Token);


                await tcs.Task;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }, cts.Token);


        var clientTask = Task.Run(async () =>
        {
            try
            {
                await using var tcpClient = await NetCodec.ConnectClientAsync(addr.ToString(), port, cts.Token);


                var pw = new PacketWriter();
                pw.WriteString("Hello World");
                using var recvPkt = pw.ToPacket();
                await tcpClient.WritePacketAsync(recvPkt.Span, cts.Token);

                using var pkt = await tcpClient.ReadPacketAsync(cts.Token);
                var pr = new PacketReader(pkt);
                var echoString = pr.ReadString();
                Assert.Equal("Hello World", echoString);

                tcs.SetResult(echoString);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }, cts.Token);

        // Await server and client task
        await Task.WhenAll(serverTask, clientTask);

        Assert.Equal("Hello World", await tcs.Task);
    }
}
//2849810897 0xA9DCA9D1