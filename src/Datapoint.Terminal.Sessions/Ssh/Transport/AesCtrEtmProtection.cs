using System.Buffers.Binary;
using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed class AesCtrEtmProtection(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initialCounter, HashKind macHash, ReadOnlySpan<byte> macKey) : IPacketProtection
{
    private readonly AesCtrCipher _cipher = new(key, initialCounter[..AesCtrCipher.BlockSize]);
    private readonly byte[] _macKey = macKey.ToArray();

    public int BlockSize => AesCtrCipher.BlockSize;

    public int TagSize => Hashing.SizeOf(macHash);

    public bool LengthCountsForAlignment => false;

    public uint ReadLength(uint sequence, ReadOnlySpan<byte> lengthField) => BinaryPrimitives.ReadUInt32BigEndian(lengthField);

    public void Seal(uint sequence, ReadOnlySpan<byte> packet, Span<byte> output)
    {
        packet[..4].CopyTo(output);
        _cipher.Transform(packet[4..], output[4..packet.Length]);
        ComputeMac(sequence, output[..packet.Length], output.Slice(packet.Length, TagSize));
    }

    public bool TryOpen(uint sequence, ReadOnlySpan<byte> wire, Span<byte> packet)
    {
        var encrypted = wire[..^TagSize];
        Span<byte> expected = stackalloc byte[TagSize];
        ComputeMac(sequence, encrypted, expected);
        if (!SecretMemory.FixedTimeEquals(expected, wire[^TagSize..]))
        {
            return false;
        }

        encrypted[..4].CopyTo(packet);
        _cipher.Transform(encrypted[4..], packet[4..encrypted.Length]);
        return true;
    }

    private void ComputeMac(uint sequence, ReadOnlySpan<byte> encrypted, Span<byte> tag)
    {
        Span<byte> sequenceBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sequenceBytes, sequence);
        using var mac = Hashing.StartHmac(macHash, _macKey);
        mac.Append(sequenceBytes).Append(encrypted).Finish(tag);
    }
}
