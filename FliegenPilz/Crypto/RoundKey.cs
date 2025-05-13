using System.Buffers.Binary;

namespace FliegenPilz.Crypto;

public readonly struct RoundKey
{
    public uint Key { get; }

    public RoundKey(uint key = 0)
    {
        Key = key;
    }
    
    public RoundKey(byte[] key)
    {
        if (key.Length != 4)
            throw new ArgumentException("Key must be 4 bytes", nameof(key));

        Key = BinaryPrimitives.ReadUInt32LittleEndian(key);
    }

    public static RoundKey GetRandom()
        => new((uint)Random.Shared.Next(0, int.MaxValue));

    /// <summary>
    /// Expands the 32-bit key into a 16-byte AES block by repeating the key 4 times.
    /// </summary>
    public void ExpandTo(Span<byte> data)
    {
        if (data.Length != 16)
            throw new ArgumentException("Destination must be 16 bytes", nameof(data));

        // Write the 4-byte key 4 times into the span
        for (var i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.Slice(i*4, 4), Key);
        }
    }

    /// <summary>
    /// Returns a new RoundKey by updating this key with the provided context logic.
    /// </summary>
    public RoundKey UpdateKey(IgContext ctx)
    {
        Span<byte> k = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(k, Key);

        uint seed = ctx.InitialSeed;
        foreach (var b in k)
        {
            seed = ctx.UpdateKey(seed, b);
        }

        return new RoundKey(seed);
    }


    public ushort GetHeaderKey()
    {
        return (ushort)(Key >> 16);
    }
}