using FliegenPilz.Net;

namespace FliegenPilz.Proto;


public interface IListLength {
    int Length { get; init; }
    
    /// <summary>
    ///  Terminal value for index list
    /// </summary>
    bool IsTerminal => Length == 0;
}

public struct I8(byte inner) : IDecodePacket<I8>, IEncodePacket, IListLength
{
    public static I8 DecodePacket(ref PacketReader reader) => new(reader.ReadByte());

    public void EncodePacket(ref PacketWriter w)
        => w.WriteByte(inner);

    public int Length
    {
        get => inner;
        init => inner = (byte)value;
    }
}

public struct I16(short inner) : IDecodePacket<I16>, IEncodePacket, IListLength
{
    public static I16 DecodePacket(ref PacketReader reader) => new(reader.ReadShort());

    public void EncodePacket(ref PacketWriter w)
        => w.WriteShort(inner);

    public int Length
    {
        get => inner;
        init => inner = (short)value;
    }
}

public struct I32(int inner) : IDecodePacket<I32>, IEncodePacket, IListLength
{
    public static I32 DecodePacket(ref PacketReader reader)
        => new(reader.ReadInt());

    public void EncodePacket(ref PacketWriter w)
        => w.WriteInt(inner);

    public int Length
    {
        get => inner;
        init => inner = value;
    }
}

public class ShroomList<L, T>(IList<T> items) : IDecodePacket<ShroomList<L, T>>, IEncodePacket
    where T : IDecodePacket<T>, IEncodePacket
    where L : IListLength, IDecodePacket<L>, IEncodePacket, new()
{
    public IList<T> Items => items;
    
    public static ShroomList<L, T> DecodePacket(ref PacketReader reader)
    {
        var len = reader.Read<L>().Length;
        var items = new List<T>(len);
        for (var i = 0; i < len; i++)
        {
            items.Add(T.DecodePacket(ref reader));
        }

        return new ShroomList<L, T>(items);
    }

    public void EncodePacket(ref PacketWriter w)
    {
        var len = new L
        {
            Length = items.Count
        };
        
        len.EncodePacket(ref w);
        foreach (var item in items)
        {
            item.EncodePacket(ref w);
        }
    }
}

public class ShroomIndexList<I, T>(IList<(I, T)> items) : IDecodePacket<ShroomIndexList<I, T>>, IEncodePacket
    where T : IDecodePacket<T>, IEncodePacket
    where I : IDecodePacket<I>, IEncodePacket, IListLength, new()
{
    public IList<(I, T)> Items => items;
    
    public static ShroomIndexList<I, T> DecodePacket(ref PacketReader reader)
    {
        var items = new List<(I, T)>();
        var ix = I.DecodePacket(ref reader);
        while (!ix.IsTerminal)
        {
            var val = T.DecodePacket(ref reader);
            items.Add((ix, val));
            ix = I.DecodePacket(ref reader);
        }
        return new ShroomIndexList<I, T>(items);
    }

    public void EncodePacket(ref PacketWriter w)
    {
        foreach (var (ix, item) in items)
        {
            ix.EncodePacket(ref w);
            item.EncodePacket(ref w);
        }
        
        // TODO use proper terminal
        new I() { Length = 255 }.EncodePacket(ref w);
    }
}