using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

internal static class UnsafePacketPod<T> where T : unmanaged
{
    private static readonly int Size = Unsafe.SizeOf<T>();

    public static T FromSpan(ReadOnlySpan<byte> span)
    {
        if (span.Length != Size)
            throw new ArgumentException($"Span must be exactly {Size} bytes", nameof(span));

        T result = default;
        ref var dest = ref Unsafe.As<T, byte>(ref result);
        span.CopyTo(MemoryMarshal.CreateSpan(ref dest, Size));
        return result;
    }

    public static T FromSequence(ReadOnlySequence<byte> seq)
    {
        if (seq.Length != Size)
            throw new InvalidOperationException($"Invalid data length for {typeof(T).Name}.");

        Span<byte> buffer = stackalloc byte[Size];
        seq.CopyTo(buffer);
        return FromSpan(buffer);
    }

    public static void EncodePacket(ref PacketWriter w, ref T value)
    {
        ref var first = ref Unsafe.As<T, byte>(ref value);
        ReadOnlySpan<byte> span = MemoryMarshal.CreateReadOnlySpan(ref first, Size);
        w.WriteBytes(span);
    }

    public static T DecodePacket(ref PacketReader r)
    {
        return FromSequence(r.ReadBytes(Size));
    }
}
