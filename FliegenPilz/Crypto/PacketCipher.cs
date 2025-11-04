using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace FliegenPilz.Crypto;

/// <summary>
/// Implements a custom stream cipher built on top of AES-256 in ECB mode, used as an OFB-like keystream generator.
/// The cipher expands a mutable <see cref="RoundKey"/> into a 16-byte seed block and repeatedly encrypts that block
/// to produce keystream segments XOR'd with the payload.
///
/// Packet layout rules handled externally (<see cref="NetCipher"/>), but this class applies the keystream with
/// a special first-fragment size: the first encrypted fragment of a packet excludes the 4-byte header so it uses
/// <c>FirstDataSegmentLength = 1456</c>. All subsequent full fragments are 1460 bytes (matching typical MTU minus headers),
/// and the tail may be shorter.
///
/// Encryption and decryption are symmetric (XOR), so <see cref="Encrypt"/> and <see cref="Decrypt"/> both delegate to the
/// same internal routine. Not thread-safe; one instance should be bound to a single connection flow.
/// </summary>
public sealed class PacketCipher : IDisposable
{
    private const int BlockSize = 16;              // AES block size bytes
    private const int FragmentSize = 1460;          // Standard data fragment size after the first
    private const int FirstDataSegmentLength = FragmentSize - 4; // Excludes 4-byte header present only before first crypt segment

    private readonly Aes _aes;
    private RoundKey _roundKey;
    private bool _disposed;

    /// <summary>
    /// Default embedded 256-bit key material used for AES ECB keystream generation.
    /// </summary>
    public static readonly byte[] DefaultKey =
    [
        0x13, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00,
        0x06, 0x00, 0x00, 0x00, 0xb4, 0x00, 0x00, 0x00,
        0x1b, 0x00, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00,
        0x33, 0x00, 0x00, 0x00, 0x52, 0x00, 0x00, 0x00,
    ];

    /// <summary>
    /// Creates a new packet cipher with a custom AES key.
    /// </summary>
    /// <param name="key">32-byte AES-256 key.</param>
    /// <param name="roundKey">Initial mutable <see cref="RoundKey"/> state.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException">If key length is not 32 bytes.</exception>
    public PacketCipher(byte[] key, RoundKey roundKey)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes for AES-256.", nameof(key));

        _aes = Aes.Create();
        _aes.Mode = CipherMode.ECB;     // We implement our own feedback; ECB used only as primitive.
        _aes.Padding = PaddingMode.None;
        _aes.Key = key;                 // key copied internally by implementation.

        _roundKey = roundKey;           // struct copy (value type) or reference as designed.
    }

    /// <summary>
    /// Creates a new packet cipher using the built-in static default key.
    /// </summary>
    /// <param name="roundKey">Initial round key.</param>
    public PacketCipher(RoundKey roundKey) : this(DefaultKey, roundKey) { }

    /// <summary>
    /// Current round key state (after each packet update).
    /// </summary>
    public RoundKey RoundKey => _roundKey;

    /// <summary>
    /// XOR-encrypts data in-place using the evolving AES-generated keystream.
    /// Equivalent to <see cref="Decrypt"/>.
    /// </summary>
    public void Encrypt(Span<byte> data) => ApplyKeystream(data);

    /// <summary>
    /// XOR-decrypts data in-place (same operation as <see cref="Encrypt"/>).
    /// </summary>
    public void Decrypt(Span<byte> data) => ApplyKeystream(data);

    /// <summary>
    /// Advances the internal <see cref="RoundKey"/> via external context (one call per packet boundary).
    /// </summary>
    public void UpdateRoundKey(IgContext ctx)
    {
        ThrowIfDisposed();
        _roundKey = _roundKey.UpdateKey(ctx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyKeystream(Span<byte> buffer)
    {
        ThrowIfDisposed();
        if (buffer.IsEmpty) return;

        var remaining = buffer;

        // First data segment excludes the 4 header bytes already handled elsewhere.
        if (remaining.Length < FirstDataSegmentLength)
        {
            ApplyOFB(_roundKey, remaining);
            return;
        }

        ApplyOFB(_roundKey, remaining[..FirstDataSegmentLength]);
        remaining = remaining[FirstDataSegmentLength..];

        while (remaining.Length > FragmentSize)
        {
            ApplyOFB(_roundKey, remaining[..FragmentSize]);
            remaining = remaining[FragmentSize..];
        }

        if (!remaining.IsEmpty)
            ApplyOFB(_roundKey, remaining);
    }

    /// <summary>
    /// Applies an OFB-like transformation: expands the round key to an initial 16-byte state, then
    /// repeatedly AES-ECB encrypts that state to generate successive keystream blocks which are XOR'd into data.
    /// </summary>
    private void ApplyOFB(RoundKey key, Span<byte> data)
    {
        // Simplicity & readability version (no unsafe). Each 16-byte keystream block is produced
        // and XOR'd in a straightforward loop; JIT removes bounds checks due to fixed BlockSize constant.
        Span<byte> block = stackalloc byte[BlockSize];
        key.ExpandTo(block); // seed material derived from round key

        int offset = 0;
        int fullBlocks = data.Length / BlockSize;
        int remainder = data.Length % BlockSize;

        for (int i = 0; i < fullBlocks; i++)
        {
            _aes.TryEncryptEcb(block, block, PaddingMode.None, out _); // evolve keystream state
            XorBlock(data.Slice(offset, BlockSize), block);
            offset += BlockSize;
        }

        if (remainder == 0)
            return;

        _aes.TryEncryptEcb(block, block, PaddingMode.None, out _);
        for (int j = 0; j < remainder; j++)
        {
            data[offset + j] ^= block[j];
        }
    }

    private static void XorBlock(Span<byte> dst, Span<byte> ks)
    {
        var d = Vector128.LoadUnsafe(ref dst[0]);
        var k = Vector128.LoadUnsafe(ref ks[0]);
        (d ^ k).StoreUnsafe(ref dst[0]);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PacketCipher));
    }

    /// <summary>
    /// Releases AES resources. After disposal further encryption calls throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _aes.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}