using System.Buffers;

namespace FliegenPilz.Net;

public struct Packet(IMemoryOwner<byte> data, int length) : IDisposable
{
    public IMemoryOwner<byte> Inner => data;

    public int Length => length;

    public void Dispose()
    {
        data.Dispose();
    }

    public short Opcode => new PacketReader(this).ReadShort();
    public ReadOnlySpan<byte> Span => data.Memory[..length].Span;

    public void CopyTo(Span<byte> packetBuf)
    {
        data.Memory.Span.CopyTo(packetBuf);
    }

    public PacketReader AsReader()
    {
        return new PacketReader(this);
    }

    public override string ToString()
    {
        var opcode = Opcode;
        var data = Span[2..Length]; // Skip the 2-byte opcode

        var hex = string.Join(" ", data.ToArray().Select(b => b.ToString("X2")));
        return $"[{opcode:X4}] {hex}";
    }

}