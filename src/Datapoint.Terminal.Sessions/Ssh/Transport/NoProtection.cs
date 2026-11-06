using System.Buffers.Binary;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed class NoProtection : IPacketProtection
{
    public static NoProtection Instance { get; } = new();

    public int BlockSize => 8;

    public int TagSize => 0;

    public bool LengthCountsForAlignment => true;

    public uint ReadLength(uint sequence, ReadOnlySpan<byte> lengthField) => BinaryPrimitives.ReadUInt32BigEndian(lengthField);

    public void Seal(uint sequence, ReadOnlySpan<byte> packet, Span<byte> output) => packet.CopyTo(output);

    public bool TryOpen(uint sequence, ReadOnlySpan<byte> wire, Span<byte> packet)
    {
        wire.CopyTo(packet);
        return true;
    }
}
