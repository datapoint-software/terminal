namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public interface IPacketProtection
{
    int BlockSize { get; }

    int TagSize { get; }

    bool LengthCountsForAlignment { get; }

    uint ReadLength(uint sequence, ReadOnlySpan<byte> lengthField);

    void Seal(uint sequence, ReadOnlySpan<byte> packet, Span<byte> output);

    bool TryOpen(uint sequence, ReadOnlySpan<byte> wire, Span<byte> packet);
}
