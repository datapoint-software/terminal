using System.Buffers.Binary;
using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public sealed class PacketTransport(Stream stream) : IDisposable
{
    public const int MaxPacketLength = 256 * 1024;
    public const int MinPadding = 4;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly byte[] _lengthBuffer = new byte[4];
    private IPacketProtection _sendProtection = NoProtection.Instance;
    private IPacketProtection _receiveProtection = NoProtection.Instance;

    public uint SendSequence { get; private set; }

    public uint ReceiveSequence { get; private set; }

    public long PacketsSentWithCurrentKeys { get; private set; }

    public long PacketsReceivedWithCurrentKeys { get; private set; }

    public long BytesSentWithCurrentKeys { get; private set; }

    public long BytesReceivedWithCurrentKeys { get; private set; }

    public void UseSendProtection(IPacketProtection protection, bool resetSequence)
    {
        _sendProtection = protection;
        PacketsSentWithCurrentKeys = 0;
        BytesSentWithCurrentKeys = 0;
        if (!resetSequence)
        {
            return;
        }

        SendSequence = 0;
    }

    public void UseReceiveProtection(IPacketProtection protection, bool resetSequence)
    {
        _receiveProtection = protection;
        PacketsReceivedWithCurrentKeys = 0;
        BytesReceivedWithCurrentKeys = 0;
        if (!resetSequence)
        {
            return;
        }

        ReceiveSequence = 0;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wire = Seal(payload.Span);
            await stream.WriteAsync(wire, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        await stream.ReadExactlyAsync(_lengthBuffer, cancellationToken).ConfigureAwait(false);
        var protection = _receiveProtection;
        var length = protection.ReadLength(ReceiveSequence, _lengthBuffer);
        ValidateLength(protection, length);
        var wire = new byte[4 + (int)length + protection.TagSize];
        _lengthBuffer.CopyTo(wire, 0);
        await stream.ReadExactlyAsync(wire.AsMemory(4), cancellationToken).ConfigureAwait(false);
        var packet = new byte[4 + (int)length];
        if (!protection.TryOpen(ReceiveSequence, wire, packet))
        {
            throw new SshProtocolException("A packet failed its integrity check.", DisconnectReason.MacError);
        }

        ReceiveSequence = NextSequence(ReceiveSequence);
        PacketsReceivedWithCurrentKeys++;
        BytesReceivedWithCurrentKeys += wire.Length;
        return ExtractPayload(packet);
    }

    private byte[] Seal(ReadOnlySpan<byte> payload)
    {
        var protection = _sendProtection;
        var packet = BuildPacket(protection, payload);
        var wire = new byte[packet.Length + protection.TagSize];
        protection.Seal(SendSequence, packet, wire);
        SendSequence = NextSequence(SendSequence);
        PacketsSentWithCurrentKeys++;
        BytesSentWithCurrentKeys += wire.Length;
        return wire;
    }

    private static byte[] BuildPacket(IPacketProtection protection, ReadOnlySpan<byte> payload)
    {
        var block = protection.BlockSize;
        var aligned = protection.LengthCountsForAlignment ? 4 + 1 + payload.Length : 1 + payload.Length;
        var padding = block - (aligned % block);
        if (padding < MinPadding)
        {
            padding += block;
        }

        var packetLength = 1 + payload.Length + padding;
        var packet = new byte[4 + packetLength];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)packetLength);
        packet[4] = (byte)padding;
        payload.CopyTo(packet.AsSpan(5));
        RandomBytes.Fill(packet.AsSpan(5 + payload.Length, padding));
        return packet;
    }

    private static void ValidateLength(IPacketProtection protection, uint length)
    {
        var aligned = protection.LengthCountsForAlignment ? length + 4 : length;
        var isValid = length is >= 1 + MinPadding and <= MaxPacketLength && aligned % (uint)protection.BlockSize == 0;
        if (isValid)
        {
            return;
        }

        throw new SshProtocolException("A packet has an invalid length.", DisconnectReason.ProtocolError);
    }

    private static byte[] ExtractPayload(byte[] packet)
    {
        var packetLength = packet.Length - 4;
        var padding = packet[4];
        if (padding < MinPadding || padding >= packetLength)
        {
            throw new SshProtocolException("A packet has invalid padding.", DisconnectReason.ProtocolError);
        }

        return packet.AsSpan(5, packetLength - 1 - padding).ToArray();
    }

    private static uint NextSequence(uint sequence)
    {
        return sequence != uint.MaxValue
            ? sequence + 1
            : throw new SshProtocolException("The packet sequence number would wrap; the connection must be rekeyed first.", DisconnectReason.ProtocolError);
    }

    public void Dispose() => _writeLock.Dispose();
}
