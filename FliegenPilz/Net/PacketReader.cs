using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FliegenPilz.Proto;

namespace FliegenPilz.Net;

/// <summary>
/// Common packet-related constants.
/// </summary>
public static class PacketConstants
{
    /// <summary>
    /// Size (in bytes) of the packet header containing the opcode.
    /// </summary>
    public const int HeaderSize = 2;
}

/// <summary>
/// Helper interface enabling fast, allocation-free conversion from a single byte
/// to an enum value without boxing. Implementations are expected to provide a
/// branchless or table-based lookup.
/// </summary>
/// <typeparam name="TEnum">The enum type to convert to.</typeparam>
public interface IEnumConverter<TEnum>
{
    /// <summary>
    /// Attempts to convert an incoming byte into <typeparamref name="TEnum"/>.
    /// </summary>
    /// <param name="value">Raw byte value.</param>
    /// <param name="result">Converted enum value if successful.</param>
    /// <returns><c>true</c> when conversion succeeded; otherwise <c>false</c>.</returns>
    static abstract bool TryFromByte(byte value, out TEnum result);
}

/// <summary>
/// A fast, ref struct based reader for decoding MapleStory-like packet formats from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <remarks>
/// The reader advances as data is consumed. Methods throw <see cref="InvalidOperationException"/>
/// when insufficient data remains (unless otherwise noted). This mirrors the behavior of <see cref="SequenceReader{T}"/>.
/// </remarks>
/// <param name="seq">The sequence representing the packet payload (including opcode at the start).</param>
public ref struct PacketReader(ReadOnlySequence<byte> seq)
{
    private SequenceReader<byte> _inner = new(seq);

    /// <summary>
    /// Returns a debug string containing the opcode (first 2 bytes) and the remaining bytes in hex.
    /// </summary>
    /// <remarks>
    /// Only the first segment of the underlying <see cref="ReadOnlySequence{T}"/> is shown. For multi-segment packets
    /// (rare in this context) subsequent segments are not included to keep allocations minimal.
    /// </remarks>
    public override string ToString()
    {
        if (_inner.CurrentSpan.Length < PacketConstants.HeaderSize)
            return "<incomplete packet header>";

        var span = _inner.CurrentSpan;
        var opcode = BinaryPrimitives.ReadUInt16LittleEndian(span);
        var data = span.Length > 2 ? span[2..] : ReadOnlySpan<byte>.Empty;

        // Avoid allocating a large string for very big packets; cap the preview.
        const int MaxPreviewBytes = 48; // 48 bytes => 3 lines-ish in logs.
        var preview = data.Length <= MaxPreviewBytes ? data : data[..MaxPreviewBytes];
        var hex = string.Join(" ", preview.ToArray().Select(static b => b.ToString("X2")));
        if (preview.Length < data.Length)
            hex += " ...";
        return $"[{opcode:X4}] {hex}";
    }


    /// <summary>
    /// Initializes a reader from an owned <see cref="Packet"/> instance.
    /// </summary>
    /// <param name="packet">The packet providing backing memory.</param>
    public PacketReader(Packet packet) : this(packet.Inner.Memory[..packet.Length]) { }

    /// <summary>
    /// Initializes a reader from a contiguous <see cref="ReadOnlyMemory{T}"/> block.
    /// </summary>
    /// <param name="memory">Memory containing the packet bytes.</param>
    public PacketReader(ReadOnlyMemory<byte> memory) : this(new ReadOnlySequence<byte>(memory)) { }

    /// <summary>
    /// Bytes remaining (not yet consumed).
    /// </summary>
    public long Remaining => _inner.Remaining;

    /// <summary>
    /// Bytes already consumed from the start of the packet.
    /// </summary>
    public long Consumed => _inner.Consumed;

    /// <summary>
    /// True when there is no more data to read.
    /// </summary>
    public bool End => _inner.End;

    /// <summary>
    /// Reads a single byte and converts it to <typeparamref name="TEnum"/> using the supplied converter.
    /// </summary>
    /// <typeparam name="TEnum">Target enum type.</typeparam>
    /// <typeparam name="TEnumConverter">Converter implementing <see cref="IEnumConverter{TEnum}"/>.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if data is insufficient or conversion fails.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEnum ReadEnum<TEnum, TEnumConverter>() where TEnumConverter : IEnumConverter<TEnum>
    {
        if (!_inner.TryRead(out var value))
            throw new InvalidOperationException("Unable to read enum byte: not enough data.");
        if (!TEnumConverter.TryFromByte(value, out var result))
            throw new InvalidOperationException($"Enum conversion failed for byte value 0x{value:X2} -> {typeof(TEnum).Name}.");
        return result;
    }

    /// <summary>
    /// Reads a 16-bit signed integer (little-endian).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadShort() => _inner.TryReadLittleEndian(out short result) ? result : throw new InvalidOperationException("Not enough data for Int16.");

    /// <summary>
    /// Reads a 16-bit unsigned integer (little-endian).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUShort() => unchecked((ushort)ReadShort());

    /// <summary>
    /// Reads a 32-bit signed integer (little-endian).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt() => _inner.TryReadLittleEndian(out int result) ? result : throw new InvalidOperationException("Not enough data for Int32.");

    /// <summary>
    /// Reads a 32-bit unsigned integer (little-endian).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt() => unchecked((uint)ReadInt());

    /// <summary>
    /// Reads a 64-bit signed integer (little-endian).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong() => _inner.TryReadLittleEndian(out long result) ? result : throw new InvalidOperationException("Not enough data for Int64.");

    /// <summary>
    /// Reads a 64-bit unsigned integer (little-endian).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadULong() => unchecked((ulong)ReadLong());

    /// <summary>
    /// Reads a Windows FILETIME value (as 64-bit) and wraps it in a <see cref="FileTime"/> struct.
    /// </summary>
    public FileTime ReadTime() => new(ReadLong());

    /// <summary>
    /// Reads exactly <paramref name="len"/> bytes as a sequence slice.
    /// </summary>
    /// <param name="len">Number of bytes to read.</param>
    /// <exception cref="InvalidOperationException">If insufficient data remains.</exception>
    public ReadOnlySequence<byte> ReadBytes(int len)
    {
        if (len < 0) throw new ArgumentOutOfRangeException(nameof(len));
        return _inner.TryReadExact(len, out var seq) ? seq : throw new InvalidOperationException($"Not enough data: requested {len} bytes, remaining {_inner.Remaining}.");
    }

    /// <summary>
    /// Reads a length-prefixed string where the length is a 16-bit little-endian signed integer.
    /// </summary>
    /// <remarks>Encoding is Latin-1 (ISO-8859-1), consistent with legacy MapleStory packet formats.</remarks>
    /// <exception cref="InvalidOperationException">If length is negative or there is insufficient data.</exception>
    public string ReadString()
    {
        var len = ReadShort();
        return len switch
        {
            < 0 => throw new InvalidOperationException("Negative string length encountered."),
            0 => string.Empty,
            _ => ReadFixedString(len)
        };
    }

    /// <summary>
    /// Reads a fixed-length string of exactly <paramref name="len"/> bytes (Latin-1).
    /// </summary>
    public string ReadFixedString(int len) => Encoding.Latin1.GetString(ReadBytes(len));

    /// <summary>
    /// Reads a 128-bit unsigned integer (little-endian).
    /// </summary>
    /// <remarks>
    /// Optimized to avoid intermediary reversing by reading the 16 bytes into a temporary stack buffer.
    /// </remarks>
    public UInt128 ReadUInt128()
    {
        // Fast path: ensure at least 16 bytes remain in the current span; else fall back to slower path.
        Span<byte> temp = stackalloc byte[16];
        if (_inner.UnreadSpan.Length >= 16)
        {
            _inner.UnreadSpan[..16].CopyTo(temp);
            _inner.Advance(16);
        }
        else
        {
            if (!_inner.TryCopyTo(temp))
                throw new InvalidOperationException("Not enough data for UInt128.");
            _inner.Advance(16);
        }
        return BinaryPrimitives.ReadUInt128LittleEndian(temp);
    }

    /// <summary>
    /// Reads a 16-bit millisecond duration and converts to <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan ReadDurMs16() => TimeSpan.FromMilliseconds(ReadShort());

    /// <summary>
    /// Reads a 32-bit millisecond duration and converts to <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan ReadDurMs32() => TimeSpan.FromMilliseconds(ReadInt());

    /// <summary>
    /// Reads a single byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte() => _inner.TryRead(out var result) ? result : throw new InvalidOperationException("Not enough data for byte.");

    /// <summary>
    /// Reads a boolean stored as a single byte (non-zero = true).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool() => ReadByte() != 0;

    /// <summary>
    /// Decodes a complex type implementing <see cref="IDecodePacket{T}"/>.
    /// </summary>
    /// <typeparam name="T">Target decoded type.</typeparam>
    public T Read<T>() where T : IDecodePacket<T> => T.DecodePacket(ref this);
}