using FliegenPilz.Proto;

namespace FliegenPilz.Net;

public class FileTime(long value): IEncodePacket, IDecodePacket<FileTime>
{
    public static readonly FileTime Zero = new(0);
    private static readonly FileTime MinValue = new(94_354_848_000_000_000); // 1/1/1900
    private static readonly FileTime MaxValue = new(150_842_304_000_000_000); // 1/1/2079

    public static FileTime FromDateTime(DateTime value) => new(value.ToFileTime());

    public DateTime ToDateTime() => DateTime.FromFileTime(value);
    public long RawValue => value;
    
    public bool IsMin => this == MinValue;
    public bool IsMax => this == MaxValue;
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteLong(value);
    }

    public static FileTime DecodePacket(ref PacketReader reader)
    {
        return new FileTime(reader.ReadLong());
    }

    public static FileTime Now()
    {
        return FromDateTime(DateTime.Now);
    }
}