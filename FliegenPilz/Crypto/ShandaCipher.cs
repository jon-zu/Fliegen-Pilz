namespace FliegenPilz.Crypto;

using System;

public static class ShandaCipher
{
    private const int ShandaRounds = 3;

    public static void Encrypt(Span<byte> data)
    {
        for (var i = 0; i < ShandaRounds; i++)
        {
            DoEvenRound<RoundEvenEncryptOp>(data);
            DoOddRound<RoundOddEncryptOp>(data);
        }
    }

    public static void Decrypt(Span<byte> data)
    {
        for (var i = 0; i < ShandaRounds; i++)
        {
            DoOddRound<RoundOddDecryptOp>(data);
            DoEvenRound<RoundEvenDecryptOp>(data);
        }
    }

    private static void DoEvenRound<TRound>(Span<byte> data)
        where TRound : struct, IRoundOp
    {
        byte state = 0;
        byte len = (byte)data.Length;
        for (int i = 0; i < data.Length; i++)
        {
            (data[i], state) = TRound.Apply(data[i], state, len--);
        }
    }

    private static void DoOddRound<TRound>(Span<byte> data)
        where TRound : struct, IRoundOp
    {
        byte state = 0;
        var len = (byte)data.Length;
        for (var i = data.Length - 1; i >= 0; i--)
        {
            (data[i], state) = TRound.Apply(data[i], state, len--);
        }
    }

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