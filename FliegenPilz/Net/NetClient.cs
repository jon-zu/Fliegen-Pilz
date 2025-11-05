using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using FliegenPilz.Crypto;

namespace FliegenPilz.Net;

/// <summary>
/// Bidirectional client abstraction handling encrypted packet IO over a <see cref="NetworkStream"/>.
/// </summary>
public class NetClient : INetworkConnection
{
    /// <summary>Maximum supported payload size (excluding 4-byte transport header).</summary>
    public const ushort MaxPacketSize = ushort.MaxValue / 2; // Keep symmetrical with reader validation.
    private const ushort MaxHandshakeSize = 128;

    private readonly NetworkStream _stream;
    private readonly NetCipher _recvCipher;
    private readonly NetCipher _sendCipher;
    private readonly IPEndPoint _remoteEndPoint;
    private readonly IPEndPoint _localEndPoint;

    private readonly byte[] _headerBuffer = new byte[4]; // Reused for every read.
    private readonly byte[] _sendBuffer = new byte[MaxPacketSize + 4]; // 4 header + payload.

    public NetClient(NetworkStream stream, NetCipher recvCipher, NetCipher sendCipher, IPEndPoint? localEndPoint = null, IPEndPoint? remoteEndPoint = null)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _recvCipher = recvCipher ?? throw new ArgumentNullException(nameof(recvCipher));
        _sendCipher = sendCipher ?? throw new ArgumentNullException(nameof(sendCipher));
        _localEndPoint = localEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
        _remoteEndPoint = remoteEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
    }

    public IPEndPoint RemoteEndPoint => _remoteEndPoint;

    public IPEndPoint LocalEndPoint => _localEndPoint;


    /// <summary>Reads and decodes an initial handshake from the remote peer.</summary>
    private static async Task<Handshake> ReadHandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        // Read Handshake size
        var headerBuffer = new byte[2];
        await stream.ReadExactlyAsync(headerBuffer, token);
        var handshakeSize = BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer);

        if (handshakeSize is 0 or > MaxHandshakeSize)
            throw new InvalidDataException("Invalid handshake size.");

        // Read Handshake
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(handshakeSize);
        var memory = memoryOwner.Memory[..handshakeSize];
        await stream.ReadExactlyAsync(memory, token);

        var pr = new PacketReader(memory);
        return Handshake.Decode(ref pr);
    }

    /// <summary>Encodes and writes a handshake to the network stream.</summary>
    private static async Task WriteHandshakeAsync(NetworkStream networkStream, Handshake handshake, CancellationToken token)
    {
        // Write Handshake with dummy size
         var pw = new PacketWriter(MaxHandshakeSize + 2);
         pw.WriteUShort(0);
         handshake.Encode(ref pw);
        
         // Convert to packet
         using var pkt = pw.ToPacket();
         
         // Overwrite dummy header
         var len = (ushort)(pkt.Length - 2);
         BinaryPrimitives.WriteUInt16LittleEndian(pkt.Inner.Memory.Span, len);
         
         // Write the packet
         await networkStream.WriteAsync(pkt.Inner.Memory, token);

    }
    
    /// <summary>Connects to a server endpoint and performs the client-side handshake.</summary>
    public static async Task<NetClient> ConnectClientAsync(string host, int port, CancellationToken token)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, token);
        var stream = tcp.GetStream();
        var handshake = await ReadHandshakeAsync(stream, token);
        var sendCipher = new NetCipher(handshake.SendKey, handshake.Version);
        var recvCipher = new NetCipher(handshake.ReceiveKey, handshake.Version.Invert());
        return new NetClient(stream, recvCipher, sendCipher,
            (IPEndPoint)tcp.Client.LocalEndPoint!,
            (IPEndPoint)tcp.Client.RemoteEndPoint!);
    }

    /// <summary>Performs the server-side accept path: write handshake then construct a <see cref="NetClient"/>.</summary>
    public static async Task<NetClient> AcceptServerAsync(TcpClient client, Handshake handshake, CancellationToken token)
    {
        var stream = client.GetStream();
        
        // Write handshake
        await WriteHandshakeAsync(stream, handshake, token);
        
        // Create ciphers
        var sendCipher = new NetCipher(handshake.ReceiveKey, handshake.Version.Invert());
        var recvCipher = new NetCipher(handshake.SendKey, handshake.Version);
        return new NetClient(stream, recvCipher, sendCipher,
            (IPEndPoint)client.Client.LocalEndPoint!,
            (IPEndPoint)client.Client.RemoteEndPoint!);
    }
    
    
    /// <summary>
    /// Reads, decrypts and returns the next packet. The caller owns and must dispose the returned packet.
    /// </summary>
    public async Task<Packet> ReadPacketAsync(CancellationToken token)
    {
        // Read header
        var headerMemory = _headerBuffer.AsMemory();
        await _stream.ReadExactlyAsync(headerMemory, token);
        var encryptedHdr = BinaryPrimitives.ReadUInt32LittleEndian(headerMemory.Span);
        var size = _recvCipher.DecryptHeader(encryptedHdr);
        
        if (size is 0 or > MaxPacketSize)
            throw new InvalidDataException("Invalid packet size.");
        
        // Read the actual packet
        var memoryOwner = MemoryPool<byte>.Shared.Rent(size);
        var memory = memoryOwner.Memory[..size];
        try
        {
            await _stream.ReadExactlyAsync(memory, token);
            _recvCipher.Decrypt(memory.Span);

            return new Packet(memoryOwner, size);
        }
        catch
        {
            memoryOwner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Encrypts and writes a packet payload. The provided span must not include the transport header.
    /// </summary>
    public ValueTask WritePacketAsync(ReadOnlySpan<byte> data, CancellationToken token)
    {
        // Write header
        var len = (ushort)data.Length;
        if (len > MaxPacketSize)
            throw new InvalidDataException("Packet size exceeds maximum.");
        
        var hdr = _sendCipher.EncryptHeader(len);
        BinaryPrimitives.WriteUInt32LittleEndian(_sendBuffer, hdr);
        
        // Copy packet after the header
        var packetBuf = _sendBuffer.AsSpan(4, len);
        data.CopyTo(packetBuf);
        
        // Encrypt the packet
        _sendCipher.Encrypt(packetBuf);
        
        return _stream.WriteAsync(_sendBuffer.AsMemory(0, len + 4), token);
    }

    /// <inheritdoc />
    public void Dispose() => _stream.Dispose();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _stream.DisposeAsync();
}
