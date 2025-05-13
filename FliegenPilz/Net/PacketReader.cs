using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace FliegenPilz.Net;

public static class PacketConstants
{
    public const int HeaderSize = 2;
}


public ref struct PacketReader(ReadOnlySequence<byte> seq)
{
    private SequenceReader<byte> _inner = new(seq);


    public PacketReader(Packet packet): this(packet.Inner.Memory)
    {
        
    }

    public PacketReader(ReadOnlyMemory<byte> memory): this(new ReadOnlySequence<byte>(memory))
    {
        
    }

    public long Remaining => _inner.Remaining;

    public short ReadShort()
    {
        return _inner.TryReadLittleEndian(out short result) ? result : throw new InvalidOperationException();
    }
    
    public ushort ReadUShort()
    {
        return unchecked((ushort)this.ReadShort());
    }
    
    public int ReadInt()
    {
        return _inner.TryReadLittleEndian(out int result) ? result : throw new InvalidOperationException();
    }
    
    public uint ReadUInt()
    {
        return unchecked((uint)this.ReadInt());
    }
    
    public long ReadLong()
    {
        return _inner.TryReadLittleEndian(out long result) ? result : throw new InvalidOperationException();
    }
    
    public ulong ReadULong()
    {
        return unchecked((ulong)this.ReadLong());
    }

    /*public FileTime ReadTime()
    {
        return new FileTime(this.ReadLong());
    }*/

    public ReadOnlySequence<byte> ReadBytes(int len)
    {
        return _inner.TryReadExact(len, out var seq) ? seq : throw new InvalidOperationException();
    }
    
    public string ReadString()
    {
        var len = ReadShort();
        return len switch
        {
            < 0 => throw new InvalidOperationException(),
            0 => string.Empty,
            _ => ReadFixedString(len)
        };
    }

    public string ReadFixedString(int len)
    {
        return Encoding.Latin1.GetString(ReadBytes(len));
    }
    
    public UInt128 ReadUInt128()
    {
        Span<int> span =
        [
            ReadInt(),
            ReadInt(),
            ReadInt(),
            ReadInt()
        ];
        span.Reverse();
        
        var bytes = MemoryMarshal.AsBytes(span);
        return BinaryPrimitives.ReadUInt128LittleEndian(bytes);
    }

    public TimeSpan ReadDurMs16()
    {
        return TimeSpan.FromMilliseconds(ReadShort());
    }

    public TimeSpan ReadDurMs32()
    {
        return TimeSpan.FromMilliseconds(ReadInt());
    }

    public byte ReadByte()
    {
        return _inner.TryRead(out byte result) ? result : throw new InvalidOperationException();
    }

    public bool ReadBool()
    {
        return this.ReadByte() != 0;
    }

    /*public T Read<T>() where T : IDecodePacket<T>
    {
        return T.DecodePacket(ref this);
    }*/
}