using System.Buffers;
using FliegenPilz.Crypto;

namespace FliegenPilz.Net;

public class Handshake
{
    public ShroomVersion Version { get; }
    public string SubVersion { get; }
    public RoundKey SendKey { get; }
    public RoundKey ReceiveKey { get; }
    public byte LocaleCode { get; }

    public Handshake(ShroomVersion version, string subVersion, RoundKey sendKey, RoundKey receiveKey, byte localeCode)
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
        var localeCode = reader.ReadByte();

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
        writer.WriteByte(LocaleCode);
    }
}