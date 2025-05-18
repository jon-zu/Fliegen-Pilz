using FliegenPilz.Crypto;

namespace FliegenPilz.Net;

public enum LocaleCode: byte
{
    Korea = 1,
    KoreaT = 2,
    Japan = 3,
    China = 4,
    ChinaT = 5,
    Taiwan = 6,
    TaiwanT = 7,
    Global = 8,
    Europe = 9,
    RlsPe = 10
}

public class Handshake
{
    private static LocaleCode LocaleFromByte(byte code)
    {
        if(code is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(code), "Locale code must be between 1 and 10");
        return (LocaleCode)code;
    }
    
    
    public ShroomVersion Version { get; }
    public string SubVersion { get; }
    public RoundKey SendKey { get; }
    public RoundKey ReceiveKey { get; }
    public LocaleCode LocaleCode { get; }

    public Handshake(ShroomVersion version, string subVersion, RoundKey sendKey, RoundKey receiveKey, LocaleCode localeCode)
    {
        Version = version;
        SubVersion = subVersion;
        SendKey = sendKey;
        ReceiveKey = receiveKey;
        LocaleCode = localeCode;
    }

    public static Handshake Decode(ref PacketReader reader)
    {
        var version = reader.ReadUShort();
        var subVersion = reader.ReadString();
        var sendKey = reader.ReadUInt();
        var receiveKey = reader.ReadUInt();
        var localeCode = LocaleFromByte(reader.ReadByte());

        // Ensure the end of the packet is reached
        if (reader.Remaining > 0)
            throw new InvalidOperationException("Packet not fully read");

        return new Handshake(new ShroomVersion(version), subVersion, new RoundKey(sendKey), new RoundKey(receiveKey),
            localeCode);
    }

    public void Encode(ref PacketWriter writer)
    {
        writer.WriteUShort(Version.Version);
        writer.WriteString(SubVersion);
        writer.WriteUInt(SendKey.Key);
        writer.WriteUInt(ReceiveKey.Key);
        writer.WriteByte((byte)LocaleCode);
    }
}