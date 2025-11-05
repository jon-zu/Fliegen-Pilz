using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

public record struct CheckPasswordReq : IDecodePacket<CheckPasswordReq>, IEncodePacket
{
    public string Id { get; set; }
    public string Password { get; set; }
    public MachineId MachineId { get; set; }
    public uint GameRoomClient { get; set; }
    public byte StartMode { get; set; }
    public byte U1 { get; set; }
    public byte U2 { get; set; }
    public uint PartnerCode { get; set; }

    public static CheckPasswordReq DecodePacket(ref PacketReader reader)
    {
        return new CheckPasswordReq
        {
            Id = reader.ReadString(),
            Password = reader.ReadString(),
            MachineId = reader.Read<MachineId>(),
            GameRoomClient = reader.ReadUInt(),
            StartMode = reader.ReadByte(),
            U1 = reader.ReadByte(),
            U2 = reader.ReadByte(),
            PartnerCode = reader.ReadUInt()
        };
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteString(Id);
        w.WriteString(Password);
        w.Write(MachineId);
        w.WriteUInt(GameRoomClient);
        w.WriteByte(StartMode);
        w.WriteByte(U1);
        w.WriteByte(U2);
        w.WriteUInt(PartnerCode);
    }
}

public record struct CheckUserLimitReq : IDecodePacket<CheckUserLimitReq>
{
    public static CheckUserLimitReq DecodePacket(ref PacketReader reader) => new();
}

public record struct SelectWorldReq : IDecodePacket<SelectWorldReq>
{
    public static SelectWorldReq DecodePacket(ref PacketReader reader) => new();
}

public record struct SelectCharRequest : IDecodePacket<SelectCharRequest>
{
    public uint CharId { get; set; }
    public HardwareInfo HardwareInfo { get; set; }

    public static SelectCharRequest DecodePacket(ref PacketReader reader)
    {
        return new SelectCharRequest
        {
            CharId = reader.ReadUInt(),
            HardwareInfo = reader.Read<HardwareInfo>()
        };
    }
}
