using System.Security.Cryptography;

namespace FliegenPilz.Crypto;

/// <summary>
/// Handles Shroom-style packet encryption/decryption composed of:
///  1. Shanda cipher transform (a variable-length XOR/byte-shuffle obfuscation)
///  2. Custom per-packet symmetric cipher using a mutable <see cref="RoundKey"/>
///  3. Rolling round key update driven by an <see cref="IgContext"/> after each packet
///
/// The class also packs / unpacks the 4-byte packet header which embeds a key-validation field
/// plus the encrypted payload length. Header layout (little-endian uint):
///  low 16 bits : headerKey ^ version
///  high 16 bits: (low16 ^ payloadLength)
/// Where headerKey is derived from the current <see cref="RoundKey"/>.
/// </summary>
public sealed class NetCipher
{
    private readonly IgContext _igContext;
    private readonly PacketCipher _packetCipher;
    private readonly ushort _version;

    /// <summary>
    /// Current version value used in header mixing.
    /// </summary>
    public ushort Version => _version;

    /// <summary>
    /// Creates a new network cipher using fully specified dependencies.
    /// </summary>
    /// <param name="igContext">Context used for <see cref="RoundKey"/> progression.</param>
    /// <param name="packetCipher">Underlying per-packet cipher instance (stateful).</param>
    /// <param name="version">Version struct containing the protocol version used in header validation.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public NetCipher(IgContext igContext, PacketCipher packetCipher, ShroomVersion version)
    {
        _igContext = igContext ?? throw new ArgumentNullException(nameof(igContext));
        _packetCipher = packetCipher ?? throw new ArgumentNullException(nameof(packetCipher));
        _version = version.Version; // value-type: cannot be null
    }

    /// <summary>
    /// Convenience constructor creating a <see cref="PacketCipher"/> from a raw <see cref="RoundKey"/>.
    /// </summary>
    /// <param name="roundKey">Initial round key.</param>
    /// <param name="version">Protocol version.</param>
    public NetCipher(RoundKey roundKey, ShroomVersion version)
        : this(IgContext.Default, new PacketCipher(roundKey), version)
    {
    }

    /// <summary>
    /// Decrypts (parses) the 4-byte packet header, validating the embedded header key
    /// and returning the payload length encoded within.
    /// </summary>
    /// <param name="hdr">Raw 4 byte little-endian header as unsigned int.</param>
    /// <returns>The decrypted payload length.</returns>
    /// <exception cref="CryptographicException">Thrown if the header key does not match the expected key.</exception>
    public ushort DecryptHeader(uint hdr)
    {
        var low = (ushort)(hdr & 0xFFFF);
        var high = (ushort)(hdr >> 16);

        var expectedKey = _packetCipher.RoundKey.GetHeaderKey();
        var extractedKey = (ushort)(low ^ _version);
        if (extractedKey != expectedKey)
            throw new CryptographicException($"Invalid header key: {extractedKey} != {expectedKey}");

        // length encoded as low ^ high
        return (ushort)(low ^ high);
    }

    /// <summary>
    /// Attempts to parse a header without throwing on key mismatch.
    /// </summary>
    /// <param name="hdr">Raw header value.</param>
    /// <param name="length">Output payload length when successful; 0 on failure.</param>
    /// <returns>True if header key was valid; otherwise false.</returns>
    public bool TryDecryptHeader(uint hdr, out ushort length)
    {
        var low = (ushort)(hdr & 0xFFFF);
        var high = (ushort)(hdr >> 16);
        var extractedKey = (ushort)(low ^ _version);
        if (extractedKey != _packetCipher.RoundKey.GetHeaderKey())
        {
            length = 0;
            return false;
        }
        length = (ushort)(low ^ high);
        return true;
    }

    /// <summary>
    /// Creates an encrypted header for a payload of the specified length using the current round key.
    /// </summary>
    /// <param name="len">Payload length to encode.</param>
    /// <returns>Packed 4-byte header.</returns>
    public uint EncryptHeader(ushort len)
    {
        var headerKey = _packetCipher.RoundKey.GetHeaderKey();
        var low = (ushort)(headerKey ^ _version);
        var high = (ushort)(low ^ len);
        return (uint)(low | (high << 16));
    }

    /// <summary>
    /// Encrypts a payload in-place. Order: Shanda -> PacketCipher -> RoundKey update.
    /// </summary>
    /// <param name="data">Mutable packet payload buffer.</param>
    public void Encrypt(Span<byte> data)
    {
        if (data.IsEmpty) return; // nothing to do
        ShandaCipher.Encrypt(data);
        _packetCipher.Encrypt(data);
        _packetCipher.UpdateRoundKey(_igContext);
    }

    /// <summary>
    /// Decrypts a payload in-place. Order: PacketCipher -> RoundKey update -> Shanda.
    /// </summary>
    /// <param name="data">Mutable packet payload buffer.</param>
    public void Decrypt(Span<byte> data)
    {
        if (data.IsEmpty) return; // nothing to do
        _packetCipher.Decrypt(data);
        _packetCipher.UpdateRoundKey(_igContext);
        ShandaCipher.Decrypt(data);
    }
}