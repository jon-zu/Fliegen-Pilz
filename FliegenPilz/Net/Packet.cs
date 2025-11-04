using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace FliegenPilz.Net;

/// <summary>
/// Represents an owned network packet whose memory comes from a pool (<see cref="IMemoryOwner{T}"/>).
/// </summary>
/// <remarks>
/// The first two bytes (little-endian) are treated as the opcode. The remaining bytes constitute the payload.
/// Instances MUST be disposed exactly once to return the underlying memory to the pool.
/// Being a struct, copies share the same underlying memory owner; accidental multiple disposal must be avoided.
/// Prefer passing by <c>ref</c> or using/consuming in one logical place to maintain clear ownership.
/// </remarks>
public struct Packet(IMemoryOwner<byte> data, int length) : IDisposable
{
    /// <summary>Underlying memory owner (may be larger than <see cref="Length"/>).</summary>
    public IMemoryOwner<byte> Inner => data;

    /// <summary>Total length of meaningful bytes stored in <see cref="Inner"/>.</summary>
    public int Length => length;

    /// <summary>Returns just the meaningful data slice as a span.</summary>
    public ReadOnlySpan<byte> Span => data.Memory.Span[..length];

    /// <summary>Returns the packet opcode (first 2 bytes, little-endian).</summary>
    public short Opcode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (length < 2) return 0;
            return BinaryPrimitives.ReadInt16LittleEndian(Span);
        }
    }

    /// <summary>Returns the payload span (data after the 2-byte opcode).</summary>
    public ReadOnlySpan<byte> PayloadSpan => length <= 2 ? ReadOnlySpan<byte>.Empty : Span[2..];

    /// <summary>Copies the packet's meaningful bytes into the destination span.</summary>
    /// <exception cref="ArgumentException">If <paramref name="destination"/> is too small.</exception>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < length)
            throw new ArgumentException("Destination span too small.", nameof(destination));
        Span.CopyTo(destination);
    }

    /// <summary>Creates a <see cref="PacketReader"/> over this packet.</summary>
    public PacketReader AsReader() => new(this);

    /// <summary>Deconstructs into opcode and payload span.</summary>
    public void Deconstruct(out short opcode, out ReadOnlySpan<byte> payload)
    {
        opcode = Opcode;
        payload = PayloadSpan;
    }

    /// <inheritdoc />
    public void Dispose() => data.Dispose();

    /// <summary>
    /// Returns a hex representation (opcode + up to <paramref name="maxBytes"/> payload bytes) for debugging.
    /// </summary>
    public string ToDebugString(int maxBytes = 64)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(Opcode.ToString("X4")).Append("] ");
        var payload = PayloadSpan;
        if (payload.Length == 0) return sb.ToString();
        var slice = payload.Length <= maxBytes ? payload : payload[..maxBytes];
        for (int i = 0; i < slice.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(slice[i].ToString("X2"));
        }
        if (slice.Length < payload.Length) sb.Append(" ...");
        return sb.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => ToDebugString();
}