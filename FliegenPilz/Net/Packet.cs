using System.Buffers;

namespace FliegenPilz.Net;

public struct Packet : IDisposable
{
    private IMemoryOwner<byte> _data;
    public IMemoryOwner<byte> Inner => _data;
    
    public int Length => _data.Memory.Length;

    public Packet(IMemoryOwner<byte> data)
    {
        _data = data;
    }
    
    public static Packet FromMemoryOwner(IMemoryOwner<byte> data)
    {
        return new Packet(data);
    }


    public void Dispose()
    {
        _data.Dispose();
    }

    public short Opcode => new PacketReader(this).ReadShort();
    public ReadOnlySpan<byte> Span => _data.Memory.Span;

    public void CopyTo(Span<byte> packetBuf)
    {
        _data.Memory.Span.CopyTo(packetBuf);
    }
}