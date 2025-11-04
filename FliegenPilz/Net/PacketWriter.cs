using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DotNext.Buffers;
using FliegenPilz.Proto;

namespace FliegenPilz.Net;

/// <summary>
/// High-performance packet writer over a growable pooled buffer.
/// </summary>
/// <remarks>
/// Mirrors <see cref="PacketReader"/> symmetry. Methods do not return success flags; they always succeed
/// (growing the underlying buffer if required) or throw. This type is a <c>ref struct</c> and therefore
/// must remain stack-only and cannot be captured or boxed.
/// </remarks>
/// <param name="initialCapacity">Initial capacity in bytes (will grow on demand).</param>
/// <param name="allocator">Optional allocator for buffer pooling.</param>
public ref struct PacketWriter(int initialCapacity = 4096, MemoryAllocator<byte>? allocator = null)
    : IDisposable
{
    private BufferWriterSlim<byte> _inner = new(initialCapacity, allocator);

    /// <summary>
    /// Detaches the written buffer from the internal writer and transfers ownership to a <see cref="MemoryOwner{T}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">If detaching fails (should be extremely rare).</exception>
    private MemoryOwner<byte> DetachBuffer() => _inner.TryDetachBuffer(out var owner)
        ? owner
        : throw new InvalidOperationException("Failed to detach buffer.");

    /// <summary>
    /// Finalizes the writer contents into an immutable <see cref="Packet"/>.
    /// </summary>
    public Packet ToPacket()
    {
        var len = _inner.WrittenCount;
        return new Packet(DetachBuffer(), len);
    }

    /// <summary>
    /// The number of bytes written so far.
    /// </summary>
    public readonly int WrittenCount => _inner.WrittenCount;

    // NOTE: A Reset operation is intentionally omitted because the underlying BufferWriterSlim<T>
    // does not expose a public reset API in the referenced version. If needed later we can
    // simulate one by detaching and re-allocating, but that would defeat pooling benefits.

    /*public void WriteOpcode(SendOpcodes opcode)
    {
        WriteShort((short)opcode);
    }*/
    
    /// <summary>Writes a 16-bit signed integer (little-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteShort(short value) => _inner.WriteLittleEndian(value);
    
    /// <summary>Writes a 16-bit unsigned integer (little-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUShort(ushort value) => WriteShort(unchecked((short)value));
    
    /// <summary>Writes a 32-bit signed integer (little-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value) => _inner.WriteLittleEndian(value);
    
    /// <summary>Writes a 32-bit unsigned integer (little-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt(uint value) => WriteInt(unchecked((int)value));
    
    /// <summary>Writes a 64-bit signed integer (little-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value) => _inner.WriteLittleEndian(value);
    
    /// <summary>Writes a 64-bit unsigned integer (little-endian).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteULong(ulong value) => WriteLong(unchecked((long)value));
    
    /// <summary>Writes a <see cref="FileTime"/> value as raw 64-bit integer.</summary>
    public void WriteTime(FileTime value) => WriteLong(value.RawValue);
    
    /// <summary>Writes a span of raw bytes verbatim.</summary>
    public void WriteBytes(ReadOnlySpan<byte> value) => _inner.Write(value);
    
    /// <summary>
    /// Writes a length-prefixed (Int16) Latin-1 string.
    /// </summary>
    /// <exception cref="ArgumentNullException">If <paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">If string length exceeds Int16 range.</exception>
    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > short.MaxValue)
            throw new InvalidOperationException($"String length {value.Length} exceeds Int16 max {short.MaxValue}.");
        WriteShort((short)value.Length);
        WriteFixedString(value);
    }
    
    /// <summary>
    /// Writes a string as raw Latin-1 bytes with no length prefix (allocation-free).
    /// </summary>
    /// <remarks>
    /// Uses span-based <see cref="Encoding.GetBytes(ReadOnlySpan{char}, Span{byte})"/> overload to avoid a temporary array.
    /// </remarks>
    public void WriteFixedString(string value)
    {
        if (string.IsNullOrEmpty(value)) return; // nothing to write
        var charCount = value.Length;
        var dest = _inner.GetSpan(charCount); // Latin-1 guarantees 1 byte per char
        var written = Encoding.Latin1.GetBytes(value.AsSpan(), dest);
        _inner.Advance(written);
    }

    /// <summary>
    /// Writes a string into a fixed-size field, null-padding (0x00) the remaining space. Ensures at least one trailing zero.
    /// </summary>
    /// <param name="value">String to write (Latin-1).</param>
    /// <param name="len">Total field length in bytes including the null padding.</param>
    /// <exception cref="InvalidOperationException">If <paramref name="value"/> does not fit with a trailing null.</exception>
    public void WriteFixedSizeString(string value, int len)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(len, 1);
        var charLen = value.Length;
        if (charLen + 1 > len)
            throw new InvalidOperationException($"String of length {charLen} will not fit into fixed field of {len} (needs space for trailing null).");

        // Write string bytes (if any)
        if (charLen > 0)
        {
            var span = _inner.GetSpan(charLen);
            var written = Encoding.Latin1.GetBytes(value.AsSpan(), span);
            _inner.Advance(written);
        }

        // Pad remaining space (including required trailing 0) with zeros.
        var pad = len - charLen;
        if (pad > 0)
        {
            var padSpan = _inner.GetSpan(pad);
            padSpan[pad..].Clear();
            _inner.Advance(pad);
        }
    }
    
    /// <summary>
    /// Writes a 128-bit unsigned integer (little-endian) matching <see cref="PacketReader.ReadUInt128"/>.
    /// </summary>
    public void WriteUInt128(UInt128 value)
    {
        Span<byte> span = stackalloc byte[16];
        BinaryPrimitives.WriteUInt128LittleEndian(span, value);
        _inner.Write(span);
    }
    
    /// <summary>Writes a <see cref="TimeSpan"/> as a 16-bit millisecond count (truncated).</summary>
    public void WriteDurMs16(TimeSpan value)
    {
        var ms = value.TotalMilliseconds;
        if (ms < short.MinValue || ms > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), $"Duration {ms}ms cannot be represented in Int16.");
        WriteShort((short)ms);
    }
    
    /// <summary>Writes a <see cref="TimeSpan"/> as a 32-bit millisecond count (truncated).</summary>
    public void WriteDurMs32(TimeSpan value)
    {
        var ms = value.TotalMilliseconds;
        if (ms < int.MinValue || ms > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), $"Duration {ms}ms cannot be represented in Int32.");
        WriteInt((int)ms);
    }

    /// <summary>Writes a single byte.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(byte value) => _inner.Add(value);

    /// <summary>Encodes a complex type implementing <see cref="IEncodePacket"/>.</summary>
    public void Write<T>(T value) where T: IEncodePacket => value.EncodePacket(ref this);

    /// <summary>Writes a boolean value as a single byte (1 = true, 0 = false).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool value) => WriteByte((byte)(value ? 1 : 0));

    /// <summary>Releases the underlying buffer (if still attached).</summary>
    public void Dispose() => _inner.Dispose();
}