using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

[InlineArray(16)]
public struct MachineId : IEncodePacket, IDecodePacket<MachineId>
{
    private byte _element0;

    public void EncodePacket(ref PacketWriter w) => UnsafePacketPod<MachineId>.EncodePacket(ref w, ref this);

    public static MachineId DecodePacket(ref PacketReader reader) =>
        UnsafePacketPod<MachineId>.DecodePacket(ref reader);
}

[InlineArray(8)]
public struct ClientKey : IEncodePacket, IDecodePacket<ClientKey>
{
    private byte _element0;

    public ClientKey(byte[] key)
    {
        if (key.Length != 8)
            throw new ArgumentException("Key must be exactly 8 bytes long.", nameof(key));

        var span = MemoryMarshal.CreateSpan(ref _element0, 8);
        key.CopyTo(span);
    }

    public static ClientKey GenerateRandom()
    {
        var key = new byte[8];
        Random.Shared.NextBytes(key);
        return new ClientKey(key);
    }

    public void EncodePacket(ref PacketWriter w) => UnsafePacketPod<ClientKey>.EncodePacket(ref w, ref this);

    public static ClientKey DecodePacket(ref PacketReader reader) =>
        UnsafePacketPod<ClientKey>.DecodePacket(ref reader);
}

[InlineArray(13)]
public struct NameString : IEncodePacket, IDecodePacket<NameString>
{
    public static NameString Empty => new([]);
    private byte _element0;

    public NameString(byte[] name)
    {
        if (name.Length != 13)
            throw new ArgumentException("Name string must be exactly 13 bytes long.", nameof(name));

        var span = MemoryMarshal.CreateSpan(ref _element0, 13);
        name.CopyTo(span);
    }

    public NameString(string name)
    {
        if (name is null)
            throw new ArgumentNullException(nameof(name));

        var bytes = Encoding.ASCII.GetBytes(name);
        if (bytes.Length > 13)
            throw new ArgumentException("Name string must be at most 13 ASCII bytes.", nameof(name));

        var span = MemoryMarshal.CreateSpan(ref _element0, 13);
        span.Clear();
        bytes.CopyTo(span);
    }

    public void EncodePacket(ref PacketWriter w) => UnsafePacketPod<NameString>.EncodePacket(ref w, ref this);

    public static NameString DecodePacket(ref PacketReader reader) =>
        UnsafePacketPod<NameString>.DecodePacket(ref reader);
}
