using System.Buffers.Binary;
using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed class ChaChaPolyProtection : IPacketProtection
{
    public const int KeySize = 64;

    private readonly byte[] _mainKey;
    private readonly byte[] _headerKey;

    public ChaChaPolyProtection(ReadOnlySpan<byte> key)
    {
        if (key.Length < KeySize)
        {
            throw new ArgumentException("chacha20-poly1305@openssh.com needs 64 bytes of key.", nameof(key));
        }

        _mainKey = key[..32].ToArray();
        _headerKey = key[32..64].ToArray();
    }

    public int BlockSize => 8;

    public int TagSize => Poly1305Mac.TagSize;

    public bool LengthCountsForAlignment => false;

    public uint ReadLength(uint sequence, ReadOnlySpan<byte> lengthField)
    {
        Span<byte> plain = stackalloc byte[4];
        ChaCha20.Transform(_headerKey, Nonce(sequence), 0, lengthField, plain);
        return BinaryPrimitives.ReadUInt32BigEndian(plain);
    }

    public void Seal(uint sequence, ReadOnlySpan<byte> packet, Span<byte> output)
    {
        var nonce = Nonce(sequence);
        ChaCha20.Transform(_headerKey, nonce, 0, packet[..4], output[..4]);
        ChaCha20.Transform(_mainKey, nonce, 1, packet[4..], output[4..packet.Length]);
        Poly1305Mac.Compute(PolyKey(nonce), output[..packet.Length], output.Slice(packet.Length, TagSize));
    }

    public bool TryOpen(uint sequence, ReadOnlySpan<byte> wire, Span<byte> packet)
    {
        var nonce = Nonce(sequence);
        var encrypted = wire[..^TagSize];
        Span<byte> expected = stackalloc byte[TagSize];
        Poly1305Mac.Compute(PolyKey(nonce), encrypted, expected);
        if (!SecretMemory.FixedTimeEquals(expected, wire[^TagSize..]))
        {
            return false;
        }

        ChaCha20.Transform(_headerKey, nonce, 0, encrypted[..4], packet[..4]);
        ChaCha20.Transform(_mainKey, nonce, 1, encrypted[4..], packet[4..encrypted.Length]);
        return true;
    }

    private static byte[] Nonce(uint sequence)
    {
        var nonce = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(nonce, sequence);
        return nonce;
    }

    private byte[] PolyKey(ReadOnlySpan<byte> nonce)
    {
        var key = new byte[Poly1305Mac.KeySize];
        ChaCha20.Transform(_mainKey, nonce, 0, new byte[Poly1305Mac.KeySize], key);
        return key;
    }
}
