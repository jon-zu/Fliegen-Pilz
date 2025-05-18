using System.Security.Cryptography;

namespace FliegenPilz.Crypto;

public class NetCipher
{
    private IgContext _igContext;
    private PacketCipher _packetCipher;
    private ushort _version;
    
    public NetCipher(IgContext igContext, PacketCipher packetCipher, ShroomVersion version)
    {
        _igContext = igContext;
        _packetCipher = packetCipher;
        _version = version.Version;
    }

    public NetCipher(RoundKey roundKey, ShroomVersion version): this(IgContext.Default, new PacketCipher(roundKey), version)
    {
        
    }


    public ushort DecryptHeader(uint hdr)
    {
        var low = (ushort)(hdr & 0xFFFF);
        var high = (ushort)(hdr >> 16);
        
        var expectedKey = _packetCipher.RoundKey.GetHeaderKey();
        var key = (low ^ _version);
        if(key != expectedKey)
            throw new CryptographicException($"Invalid header key: {key} != {expectedKey}");
        
        
        return (ushort)(low ^ high); //TODO
    }

    public uint EncryptHeader(ushort len)
    {
        var key = _packetCipher.RoundKey.GetHeaderKey();
        var low = (ushort)(key ^ _version);
        var high = (ushort)(low ^ len);
        return (uint)(low | (high << 16));
    }

    public void Encrypt(Span<byte> data)
    {
        ShandaCipher.Encrypt(data);
        _packetCipher.Encrypt(data);
        _packetCipher.UpdateRoundKey(_igContext);
    }

    public void Decrypt(Span<byte> data)
    {
        _packetCipher.Decrypt(data);
        _packetCipher.UpdateRoundKey(_igContext);
        ShandaCipher.Decrypt(data);
    }
}