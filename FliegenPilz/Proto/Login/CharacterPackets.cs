using System;
using System.Collections.Generic;
using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

public record struct ItemId(int Id) : IEncodePacket, IDecodePacket<ItemId>
{
    public void EncodePacket(ref PacketWriter w) => w.WriteInt(Id);

    public static ItemId DecodePacket(ref PacketReader reader) => new(reader.ReadInt());
}

public record struct AvatarEquips : IEncodePacket
{
    public AvatarEquips()
    {
        EquipIds = new ShroomIndexList<I8, ItemId>(new List<(I8, ItemId)>());
        MaskedEquipIds = new ShroomIndexList<I8, ItemId>(new List<(I8, ItemId)>());
        WeaponStickerId = new ItemId(0);
    }

    public ShroomIndexList<I8, ItemId> EquipIds { get; set; }
    public ShroomIndexList<I8, ItemId> MaskedEquipIds { get; set; }
    public ItemId WeaponStickerId { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        EquipIds.EncodePacket(ref w);
        MaskedEquipIds.EncodePacket(ref w);
        WeaponStickerId.EncodePacket(ref w);
    }
}

public record struct AvatarData : IEncodePacket
{
    public Gender Gender { get; set; }
    public byte Skin { get; set; }
    public uint Face { get; set; }
    public bool Mega { get; set; }
    public uint Hair { get; set; }
    public AvatarEquips Equips { get; set; }
    public uint PetItem1 { get; set; }
    public uint PetItem2 { get; set; }
    public uint PetItem3 { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte((byte)Gender);
        w.WriteByte(Skin);
        w.WriteUInt(Face);
        w.WriteBool(Mega);
        w.WriteUInt(Hair);
        Equips.EncodePacket(ref w);
        w.WriteUInt(PetItem1);
        w.WriteUInt(PetItem2);
        w.WriteUInt(PetItem3);
    }
}

public record struct CharStat : IEncodePacket
{
    public uint Id { get; set; }
    public NameString Name { get; set; }
    public Gender Gender { get; set; }
    public byte Skin { get; set; }
    public uint Face { get; set; }
    public uint Hair { get; set; }
    public ulong Pet1 { get; set; }
    public ulong Pet2 { get; set; }
    public ulong Pet3 { get; set; }
    public byte Level { get; set; }
    public ushort Job { get; set; }
    public ushort Str { get; set; }
    public ushort Dex { get; set; }
    public ushort Int { get; set; }
    public ushort Luk { get; set; }
    public uint Hp { get; set; }
    public uint MaxHp { get; set; }
    public uint Mp { get; set; }
    public uint MaxMp { get; set; }
    public ushort Ap { get; set; }
    public ushort Sp { get; set; }
    public int Exp { get; set; }
    public ushort Fame { get; set; }
    public uint TempExp { get; set; }
    public uint FieldId { get; set; }
    public byte Portal { get; set; }
    public uint PlayTime { get; set; }
    public ushort SubJob { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(Id);
        w.Write(Name);
        w.WriteByte((byte)Gender);
        w.WriteByte(Skin);
        w.WriteUInt(Face);
        w.WriteUInt(Hair);
        w.WriteULong(Pet1);
        w.WriteULong(Pet2);
        w.WriteULong(Pet3);
        w.WriteByte(Level);
        w.WriteUShort(Job);
        w.WriteUShort(Str);
        w.WriteUShort(Dex);
        w.WriteUShort(Int);
        w.WriteUShort(Luk);
        w.WriteUInt(Hp);
        w.WriteUInt(MaxHp);
        w.WriteUInt(Mp);
        w.WriteUInt(MaxMp);
        w.WriteUShort(Ap);
        w.WriteUShort(Sp);
        w.WriteInt(Exp);
        w.WriteUShort(Fame);
        w.WriteUInt(TempExp);
        w.WriteUInt(FieldId);
        w.WriteByte(Portal);
        w.WriteUInt(PlayTime);
        w.WriteUShort(SubJob);
    }
}

public record struct RankInfo : IEncodePacket
{
    public uint WorldRank { get; set; }
    public uint RankMove { get; set; }
    public uint JobRank { get; set; }
    public uint JobRankMove { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(WorldRank);
        w.WriteUInt(RankMove);
        w.WriteUInt(JobRank);
        w.WriteUInt(JobRankMove);
    }
}

public record struct CharView : IEncodePacket
{
    public CharStat Stats { get; set; }
    public AvatarData Avatar { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        Stats.EncodePacket(ref w);
        Avatar.EncodePacket(ref w);
    }
}

public record CharRankView : IEncodePacket, IDecodePacket<CharRankView>
{
    public CharView View { get; set; }
    public RankInfo? RankInfo { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        View.EncodePacket(ref w);
        w.WriteByte(0);

        if (RankInfo is { } info)
        {
            w.WriteByte(1);
            info.EncodePacket(ref w);
        }
        else
        {
            w.WriteByte(0);
        }
    }

    public static CharRankView DecodePacket(ref PacketReader reader) => throw new NotImplementedException();
}

public record struct HardwareInfo : IDecodePacket<HardwareInfo>
{
    public string MacAddress { get; set; }
    public string HddSerialNumber { get; set; }

    public static HardwareInfo DecodePacket(ref PacketReader reader)
    {
        return new HardwareInfo
        {
            MacAddress = reader.ReadString(),
            HddSerialNumber = reader.ReadString()
        };
    }
}

public record struct MigrateInfo : IEncodePacket
{
    public uint Address4 { get; set; }
    public ushort Port { get; set; }
    public uint CharId { get; set; }
    public bool Premium { get; set; }
    public uint PremiumArgument { get; set; }
    public ulong ClientSessionId { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(Address4);
        w.WriteUShort(Port);
        w.WriteUInt(CharId);
        w.WriteBool(Premium);
        w.WriteUInt(PremiumArgument);
        w.WriteULong(ClientSessionId);
    }
}
