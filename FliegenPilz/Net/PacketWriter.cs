using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using DotNext.Buffers;
using FliegenPilz.Proto;

namespace FliegenPilz.Net;

public ref struct PacketWriter(int initialCapacity = 4096, MemoryAllocator<byte>? allocator = null)
    : IDisposable
{
    private BufferWriterSlim<byte> _inner = new(initialCapacity, allocator);

    private MemoryOwner<byte> DetachBuffer()
    {
        return _inner.TryDetachBuffer(out var owner) ? owner : throw new InvalidOperationException();
    }

    public Packet ToPacket()
    {
        var len = _inner.WrittenCount;
        return new Packet(DetachBuffer(), len);
    }

    /*public void WriteOpcode(SendOpcodes opcode)
    {
        WriteShort((short)opcode);
    }*/
    
    public void WriteShort(short value)
    {
        _inner.WriteLittleEndian(value);
    }
    
    public void WriteUShort(ushort value)
    {
        WriteShort(unchecked((short)value));
    }
    
    public void WriteInt(int value)
    {
        _inner.WriteLittleEndian(value);
    }
    
    public void WriteUInt(uint value)
    {
        WriteInt(unchecked((int)value));
    }
    
    public void WriteLong(long value)
    {
        _inner.WriteLittleEndian(value);
    }
    
    public void WriteULong(ulong value)
    {
        WriteLong(unchecked((long)value));
    }
    
    public void WriteTime(FileTime value)
    {
        WriteLong(value.RawValue);
    }
    
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        _inner.Write(value);
    }
    
    public void WriteString(string value)
    {
        WriteShort((short)value.Length);
        WriteFixedString(value);
    }
    
    public void WriteFixedString(string value)
    {
        WriteBytes(Encoding.Latin1.GetBytes(value));
    }

    public void WriteFixedSizeString(string value, int len)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        if(bytes.Length + 1 > len)
            throw new InvalidOperationException();

        WriteBytes(bytes);
        for(var i = bytes.Length; i < len; i++)
            WriteByte(0);
    }
    
    public void WriteUInt128(UInt128 value)
    {
        Span<byte> span = stackalloc byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(span, value);
        
        var data = MemoryMarshal.Cast<byte, int>(span);
        for(var i = 0; i < 4; i++)
            WriteInt(data[4 - i - 1]);
    }
    
    public void WriteDurMs16(TimeSpan value)
    {
        WriteShort((short)value.TotalMilliseconds);
    }
    
    public void WriteDurMs32(TimeSpan value)
    {
        WriteInt((int)value.TotalMilliseconds);
    }

    public void WriteByte(byte value)
    {
        _inner.Add(value);
    }

    public void Write<T>(T value) where T: IEncodePacket
    {
        value.EncodePacket(ref this);
    }

    public void WriteBool(bool value)
    {
        WriteByte((byte)(value ? 1 : 0));
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}