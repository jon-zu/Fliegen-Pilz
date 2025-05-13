using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using FliegenPilz.Crypto;

namespace FliegenPilz.Net;

public class NetCodec: IDisposable, IAsyncDisposable
{
    
    private const ushort MaxPacketSize = ushort.MaxValue / 2;
    private const ushort MaxHandshakeSize = 128;
    
    private readonly NetworkStream _stream;
    private readonly NetCipher _recvCipher;
    private readonly NetCipher _sendCipher;

    private readonly byte[] _headerBuffer = new byte[4];
    private readonly byte[] _sendBuffer = new byte[MaxPacketSize + 4];

    public NetCodec(NetworkStream stream, NetCipher recvCipher, NetCipher sendCipher)
    {
        _stream = stream;
        _recvCipher = recvCipher;
        _sendCipher = sendCipher;
    }


    static async Task<Handshake> ReadHandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        // Read Handshake size
        var headerBuffer = new byte[2];
        await stream.ReadExactlyAsync(headerBuffer, token);
        var handshakeSize = BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer);

        if (handshakeSize is 0 or > MaxHandshakeSize)
            throw new Exception("Invalid Handshake size");

        // Read Handshake
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(handshakeSize);
        var memory = memoryOwner.Memory[..handshakeSize];
        await stream.ReadExactlyAsync(memory, token);

        var pr = new PacketReader(memory);
        return Handshake.Decode(ref pr);
    }

    static async Task WriteHandshakeAsync(NetworkStream networkStream, Handshake handshake, CancellationToken token)
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
    
    public static  async Task<NetCodec> ConnectClientAsync(string host, int port, CancellationToken token)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port, token);
        
        // Read handshake
        var handshake = await ReadHandshakeAsync(client.GetStream(), token);
        var sendCipher = new NetCipher(handshake.SendKey, handshake.Version.Version);
        var recvCipher = new NetCipher(handshake.ReceiveKey, handshake.Version.Invert().Version);
        
        return new NetCodec(client.GetStream(), recvCipher, sendCipher);
    }

    public static async Task<NetCodec> AcceptServerAsync(TcpClient client, Handshake handshake, CancellationToken token)
    {
        var stream = client.GetStream();
        
        // Write handshake
        await WriteHandshakeAsync(stream, handshake, token);
        
        // Create ciphers
        var sendCipher = new NetCipher(handshake.ReceiveKey, handshake.Version.Invert().Version);
        var recvCipher = new NetCipher(handshake.SendKey, handshake.Version.Version);
        return new NetCodec(stream, recvCipher, sendCipher);
    }
    
    
    public async Task<Packet> ReadPacketAsync(CancellationToken token)
    {
        // Read header
        var headerMemory = _headerBuffer.AsMemory();
        await _stream.ReadExactlyAsync(headerMemory, token);
        var encryptedHdr = BinaryPrimitives.ReadUInt32LittleEndian(headerMemory.Span);
        var size = _recvCipher.DecryptHeader(encryptedHdr);
        
        if(size is 0 or > ushort.MaxValue / 2)
            throw new Exception("Packet size exceeds maximum packet size");
        
        // Read the actual packet
        var memoryOwner = MemoryPool<byte>.Shared.Rent(size);
        var memory = memoryOwner.Memory[..size];
        try
        {
            await _stream.ReadExactlyAsync(memory, token);
            _recvCipher.Decrypt(memory.Span);

            return Packet.FromMemoryOwner(memoryOwner);
        }
        catch
        {
            memoryOwner.Dispose();
            throw;
        }
    }

    public ValueTask WritePacketAsync(ReadOnlySpan<byte> data, CancellationToken token)
    {
        // Write header
        var len = (ushort)data.Length;
        if(len > MaxPacketSize)
            throw new Exception("Packet size exceeds maximum packet size");
        
        var hdr = _sendCipher.EncryptHeader(len);
        BinaryPrimitives.WriteUInt32LittleEndian(_sendBuffer, hdr);
        
        // Copy packet after the header
        var packetBuf = _sendBuffer.AsSpan(4, len);
        data.CopyTo(packetBuf);
        
        // Encrypt the packet
        _sendCipher.Encrypt(packetBuf);
        
        return _stream.WriteAsync(_sendBuffer.AsMemory(0, len + 4), token);
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
    }
}