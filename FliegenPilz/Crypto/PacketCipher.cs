using System.Security.Cryptography;

namespace FliegenPilz.Crypto;

public sealed class PacketCipher : IDisposable
{
    private const int BlockSize = 16;
    private const int BlockLen = 1460;
    private const int FirstBlockLen = BlockLen - 4;

    private readonly Aes _aes;
    private RoundKey _roundKey;

    public static readonly byte[] DefaultKey =
    [
        0x13, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00,
        0x06, 0x00, 0x00, 0x00, 0xb4, 0x00, 0x00, 0x00,
        0x1b, 0x00, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00,
        0x33, 0x00, 0x00, 0x00, 0x52, 0x00, 0x00, 0x00,
    ];

    public PacketCipher(byte[] key, RoundKey roundKey)
    {
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes for AES-256.");

        _aes = Aes.Create();
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;
        _aes.Key = key;

        _roundKey = roundKey;
    }
    
    public PacketCipher(RoundKey roundKey) : this(DefaultKey, roundKey)
    {
    }
    
    public RoundKey RoundKey => _roundKey;

    public void Encrypt(Span<byte> data)
    {
        ApplyKeystream(data);
    }

    public void Decrypt(Span<byte> data)
    {
        ApplyKeystream(data);
    }

    public void UpdateRoundKey(IgContext ctx)
    {
        _roundKey = _roundKey.UpdateKey(ctx);
    }

    private void ApplyKeystream(Span<byte> buffer)
    {
        var remaining = buffer;

        if (remaining.Length < FirstBlockLen)
        {
            ApplyOFB(_roundKey, remaining);
            return;
        }

        ApplyOFB(_roundKey, remaining[..FirstBlockLen]);
        remaining = remaining[FirstBlockLen..];

        while (remaining.Length > BlockLen)
        {
            ApplyOFB(_roundKey, remaining[..BlockLen]);
            remaining = remaining[BlockLen..];
        }

        if (remaining.IsEmpty)
            return;
        
        ApplyOFB(_roundKey, remaining);
    }

    private void ApplyOFB(RoundKey key, Span<byte> data)
    {
        Span<byte> block = stackalloc byte[16];
        key.ExpandTo(block);
        
        var offset = 0;
        var blocks = data.Length / BlockSize;
        var remainder = data.Length % BlockSize;

        for (var i = 0; i < blocks; i++)
        {
            _aes.TryEncryptEcb(block, block, PaddingMode.None, out _);
            for (var j = 0; j < BlockSize; j++)
            {
                data[offset + j] ^= block[j];
            }
            offset += BlockSize;
        }

        if (remainder <= 0)
            return;

        _aes.TryEncryptEcb(block, block, PaddingMode.None, out _);
        for (var j = 0; j < remainder; j++)
        {
            data[offset + j] ^= block[j];
        }
    }


    void IDisposable.Dispose()
    {
        _aes.Dispose();
    }
}