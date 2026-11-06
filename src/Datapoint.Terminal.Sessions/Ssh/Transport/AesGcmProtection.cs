using System.Buffers.Binary;
using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed class AesGcmProtection(ReadOnlySpan<byte> key, ReadOnlySpan<byte> initialNonce) : IPacketProtection, IDisposable
{
    private readonly AesGcmCipher _cipher = new(key);
    private readonly byte[] _nonce = initialNonce[..AesGcmCipher.NonceSize].ToArray();

    public int BlockSize => 16;

    public int TagSize => AesGcmCipher.TagSize;

    public bool LengthCountsForAlignment => false;

    public uint ReadLength(uint sequence, ReadOnlySpan<byte> lengthField) => BinaryPrimitives.ReadUInt32BigEndian(lengthField);

    public void Seal(uint sequence, ReadOnlySpan<byte> packet, Span<byte> output)
    {
        packet[..4].CopyTo(output);
        var body = packet[4..];
        _cipher.Encrypt(_nonce, body, output.Slice(4, body.Length), output.Slice(4 + body.Length, TagSize), packet[..4]);
        IncrementInvocationCounter();
    }

    public bool TryOpen(uint sequence, ReadOnlySpan<byte> wire, Span<byte> packet)
    {
        var bodyLength = wire.Length - 4 - TagSize;
        wire[..4].CopyTo(packet);
        var opened = _cipher.TryDecrypt(_nonce, wire.Slice(4, bodyLength), wire.Slice(4 + bodyLength, TagSize), packet.Slice(4, bodyLength), wire[..4]);
        IncrementInvocationCounter();
        return opened;
    }

    public void Dispose() => _cipher.Dispose();

    private void IncrementInvocationCounter()
    {
        var counter = BinaryPrimitives.ReadUInt64BigEndian(_nonce.AsSpan(4));
        BinaryPrimitives.WriteUInt64BigEndian(_nonce.AsSpan(4), unchecked(counter + 1));
    }
}
