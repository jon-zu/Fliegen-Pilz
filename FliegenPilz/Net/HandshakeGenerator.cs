using FliegenPilz.Crypto;

namespace FliegenPilz.Net;

public class HandshakeGenerator(ShroomVersion version, string subVersion, LocaleCode localeCode)
{
    public Handshake GenerateHandshake()
    {
        return new Handshake(version, subVersion, RoundKey.GetRandom(), RoundKey.GetRandom(), localeCode);
    }
}