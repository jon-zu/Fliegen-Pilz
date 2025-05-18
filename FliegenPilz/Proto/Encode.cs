using FliegenPilz.Net;

namespace FliegenPilz.Proto;

public interface IDecodePacket<out TSelf>  where TSelf : IDecodePacket<TSelf> {
    static abstract TSelf DecodePacket(ref PacketReader reader);
}

public interface IEncodePacket {
    void EncodePacket(ref PacketWriter w);
}

public interface IPacketMessage : IEncodePacket
{
    public static abstract SendOpcodes Opcode { get; }   
}