using System.Collections.Generic;
using FliegenPilz.Data;
using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

public static class LoginPackets
{
    public static PacketWriter WriteSelectWorld(this PacketWriter writer, IEnumerable<CharacterEntity> characters, LoginOption option = LoginOption.NoSecondaryPassword1)
    {
        var response = characters.ToCharacterListResponse(option);
        response.EncodePacket(ref writer);
        return writer;
    }
}
