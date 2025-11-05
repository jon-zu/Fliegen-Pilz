using System.Collections.Generic;
using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

public record struct WorldItem : IEncodePacket
{
    public static readonly WorldItem End = new("End");

    public WorldItem(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public WorldState State { get; set; } = WorldState.Normal;
    public byte EventDescription { get; set; }
    public byte EventExp { get; set; }
    public List<ChannelItem> Channels { get; set; } = new();

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0);
        w.WriteString(Name);
        w.WriteByte((byte)State);
        w.WriteByte(EventDescription);
        w.WriteByte(EventExp);
        w.WriteByte((byte)Channels.Count);
        foreach (var channel in Channels)
        {
            channel.EncodePacket(ref w);
        }
    }
}

public record struct ChannelItem : IEncodePacket
{
    public ChannelItem(string name, byte worldId, byte index)
    {
        Name = name;
        WorldId = worldId;
        Index = index;
    }

    public string Name { get; set; }
    public byte WorldId { get; set; }
    public byte Index { get; set; }
    public int UserCount { get; set; }
    public int MaxUsers { get; set; } = 100;

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteString(Name);
        w.WriteInt(UserCount);
        w.WriteInt(MaxUsers);
        w.WriteByte(WorldId);
        w.WriteByte(Index);
        w.WriteBool(true);
    }
}

public record struct LoginInfo : IEncodePacket
{
    public ClientKey ClientKey { get; set; }
    public LoginOption LoginOpt { get; set; }
    public bool SkipPin { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.Write(ClientKey);
        w.WriteByte((byte)LoginOpt);
        w.WriteBool(SkipPin);
    }
}
