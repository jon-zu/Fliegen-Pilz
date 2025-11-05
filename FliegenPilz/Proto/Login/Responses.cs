using System.Collections.Generic;
using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

public record struct AccountGrade(GradeCode Code, SubGradeCode SubCode) : IEncodePacket
{
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte((byte)Code);
        w.WriteUShort((ushort)SubCode);
    }
}

public struct AccountInfo : IEncodePacket
{
    public uint Id { get; set; }
    public Gender Gender { get; set; }
    public AccountGrade Grade { get; set; }
    public byte CountryId { get; set; }
    public string Name { get; set; }
    public byte PurchaseExp { get; set; }
    public byte ChatBlockReason { get; set; }
    public FileTime ChatBlockDate { get; set; }
    public FileTime RegistrationDate { get; set; }
    public uint NumChars { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(Id);
        w.WriteByte((byte)Gender);
        w.Write(Grade);
        w.WriteByte(CountryId);
        w.WriteString(Name);
        w.WriteByte(PurchaseExp);
        w.WriteByte(ChatBlockReason);
        w.Write(ChatBlockDate);
        w.Write(RegistrationDate);
        w.WriteUInt(NumChars);
    }
}

public record struct LoginResultHeader : IEncodePacket
{
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0);
        w.WriteByte(0);
        w.WriteByte(0);
    }
}

public record struct CheckUserLimitResponse : IPacketMessage
{
    public static SendOpcodes Opcode { get; } = SendOpcodes.CheckUserLimitResult;

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0);
    }
}

public record struct WorldInformationResponse : IPacketMessage
{
    public uint WorldId { get; set; }
    public WorldItem World { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0);
        w.WriteUInt(WorldId);
        World.EncodePacket(ref w);
    }

    public static readonly WorldInformationResponse End = new()
    {
        WorldId = uint.MaxValue,
        World = WorldItem.End
    };

    public static SendOpcodes Opcode { get; } = SendOpcodes.WorldInformation;
}

public record struct CheckPasswordSuccess : IPacketMessage
{
    public LoginResultHeader Hdr { get; set; }
    public AccountInfo AccountInfo { get; set; }
    public LoginInfo Info { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        Hdr.EncodePacket(ref w);
        AccountInfo.EncodePacket(ref w);
        Info.EncodePacket(ref w);
    }

    public static SendOpcodes Opcode { get; } = SendOpcodes.CheckPasswordResult;
}

public record struct SelectCharResponse : IPacketMessage
{
    public MigrateInfo MigrateInfo { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0);
        w.WriteByte(0);
        MigrateInfo.EncodePacket(ref w);
    }

    public static SendOpcodes Opcode { get; } = SendOpcodes.SelectCharacterResult;
}

public record struct CharViewListResp : IPacketMessage
{
    public CharViewListResp()
    {
        Characters = new ShroomList<I8, CharRankView>(new List<CharRankView>());
        LoginOption = LoginOption.NoSecondaryPassword1;
    }

    public ShroomList<I8, CharRankView> Characters { get; set; }
    public LoginOption LoginOption { get; set; }
    public uint Slots { get; set; }
    public uint BuySlots { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0);
        Characters.EncodePacket(ref w);
        w.WriteByte((byte)LoginOption);
        w.WriteUInt(Slots);
        w.WriteUInt(BuySlots);
    }

    public static SendOpcodes Opcode { get; } = SendOpcodes.SelectWorldResult;
}
