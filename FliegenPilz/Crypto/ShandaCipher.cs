namespace FliegenPilz.Crypto;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Implements the classic MapleStory "Shanda" packet obfuscation transform.
/// This is a lightweight, reversible byte-wise scrambling algorithm – NOT cryptographically secure.
///
/// Algorithm overview:
///  • Performs <see cref="ShandaRounds"/> rounds (historically 3) of two passes:
///     1. An "even" forward pass (index 0 → N-1)
///     2. An "odd" backward pass (index N-1 → 0)
///  • Each pass updates a rolling state byte and applies rotates, adds, XOR, invert and constants.
///  • The length value is decremented each byte and intentionally truncated to 8 bits (byte wrap) –
///    thus packets longer than 255 bytes wrap their length counter semantics every 256 steps.
///
/// Design notes:
///  • Generics + static abstract interface methods allow the JIT to specialize each pass; because the
///    round types are value types (<c>struct</c>), the JIT can devirtualize and inline <c>Apply</c> bodies.
///  • Value-level overflow and wrapping behavior is preserved to match legacy protocol logic.
/// </summary>
public static class ShandaCipher
{
    /// <summary>Number of full (even+odd) round pairs applied for each Encrypt/Decrypt.</summary>
    private const int ShandaRounds = 3;

    /// <summary>
    /// Encrypts (scrambles) the span in-place. Symmetric with <see cref="Decrypt"/>.
    /// </summary>
    /// <param name="data">Packet payload buffer (modified in-place).</param>
    public static void Encrypt(Span<byte> data)
    {
        if (data.IsEmpty) return;
        for (var i = 0; i < ShandaRounds; i++)
        {
            DoEvenRound<RoundEvenEncryptOp>(data);
            DoOddRound<RoundOddEncryptOp>(data);
        }
    }

    /// <summary>
    /// Decrypts (unscrambles) the span in-place restoring the original bytes.
    /// </summary>
    /// <param name="data">Packet payload buffer (modified in-place).</param>
    public static void Decrypt(Span<byte> data)
    {
        if (data.IsEmpty) return;
        for (var i = 0; i < ShandaRounds; i++)
        {
            DoOddRound<RoundOddDecryptOp>(data);
            DoEvenRound<RoundEvenDecryptOp>(data);
        }
    }

    /// <summary>
    /// Forward (left-to-right) pass for a given round operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DoEvenRound<TRound>(Span<byte> data)
        where TRound : struct, IRoundOp
    {
        byte state = 0;
        // length intentionally truncated to byte for protocol-accurate wrap-around.
        byte len = (byte)data.Length;
        for (int i = 0; i < data.Length; i++)
        {
            (data[i], state) = TRound.Apply(data[i], state, len--); // len after use
        }
    }

    /// <summary>
    /// Backward (right-to-left) pass for a given round operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DoOddRound<TRound>(Span<byte> data)
        where TRound : struct, IRoundOp
    {
        byte state = 0;
        byte len = (byte)data.Length;
        // Use decrement-test pattern to avoid signed underflow branch misprediction.
        for (int i = data.Length; i-- > 0;)
        {
            (data[i], state) = TRound.Apply(data[i], state, len--);
        }
    }

    /// <summary>
    /// Contract for a single per-byte round transformation. Implementations must be pure and deterministic.
    /// </summary>
    private interface IRoundOp
    {
        static abstract (byte result, byte nextState) Apply(byte b, byte state, byte len);
    }

    private struct RoundEvenEncryptOp : IRoundOp
    {
        public static (byte, byte) Apply(byte b, byte state, byte len)
        {
            b = byte.RotateLeft(b, 3);
            b = (byte)(b + len);
            var nextState = b;
            b ^= state;
            b = byte.RotateRight(b, len);
            b = (byte)~b;
            b = (byte)(b + 0x48);
            return (b, (byte)(nextState ^ state));
        }
    }

    private struct RoundEvenDecryptOp : IRoundOp
    {
        public static (byte, byte) Apply(byte b, byte state, byte len)
        {
            b = (byte)(b - 0x48);
            b = (byte)~b;
            b = byte.RotateLeft(b, len);
            var nextState = b;
            b ^= state;
            b = (byte)(b - len);
            b = byte.RotateRight(b, 3);
            return (b, nextState);
        }
    }

    private struct RoundOddEncryptOp : IRoundOp
    {
        public static (byte, byte) Apply(byte b, byte state, byte len)
        {
            b = byte.RotateLeft(b, 4);
            b = (byte)(b + len);
            var nextState = b;
            b ^= state;
            b ^= 0x13;
            b = byte.RotateRight(b, 3);
            return (b, (byte)(nextState ^ state));
        }
    }

    private struct RoundOddDecryptOp : IRoundOp
    {
        public static (byte, byte) Apply(byte b, byte state, byte len)
        {
            b = byte.RotateLeft(b, 3);
            b ^= 0x13;
            var nextState = b;
            b ^= state;
            b = (byte)(b - len);
            b = byte.RotateRight(b, 4);
            return (b, nextState);
        }
    }
}